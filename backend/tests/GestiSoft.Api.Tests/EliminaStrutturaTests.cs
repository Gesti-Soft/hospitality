using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Entities.Riferimenti;
using GestiSoft.Infrastructure.Persistence;
using GestiSoft.Infrastructure.Repositories;
using GestiSoft.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestiSoft.Api.Tests;

/// <summary>Salta il test se non c'è un Postgres su cui creare un database usa e getta.</summary>
public sealed class FactConPostgresAttribute : FactAttribute
{
    public const string VariabileAmbiente = "GESTISOFT_TEST_POSTGRES";

    public FactConPostgresAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VariabileAmbiente)))
        {
            Skip = $"Imposta {VariabileAmbiente} con la connection string di un Postgres (il database indicato non viene toccato: se ne crea uno temporaneo).";
        }
    }
}

/// <summary>
/// L'eliminazione definitiva di una Struttura deve cancellare tutto ciò che le appartiene. Il test
/// crea un database nuovo, ci mette una Struttura con almeno una riga in OGNI tabella non globale,
/// la elimina e controlla che quelle tabelle siano vuote. Una tabella aggiunta in futuro fa fallire
/// il controllo iniziale finché non entra qui e in SuperAdminRepository.EliminaStrutturaAsync.
/// </summary>
public class EliminaStrutturaTests
{
    /// <summary>Tabelle che non appartengono a una Struttura: restano dopo l'eliminazione.</summary>
    private static readonly HashSet<Type> TabelleGlobali =
    [
        typeof(Cliente), typeof(Utente), typeof(CodiceRecuperoUtente), typeof(DispositivoFidato),
        typeof(ImpostazioniGlobali), typeof(Comune), typeof(Stato), typeof(Documento), typeof(TipoAlloggiato),
    ];

    [FactConPostgres]
    public async Task Elimina_ogni_riga_della_struttura_e_restituisce_le_foto_ancora_sul_disco()
    {
        var server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(FactConPostgresAttribute.VariabileAmbiente));
        var nomeDatabase = $"gestisoft_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(server.ConnectionString))
        {
            await admin.OpenAsync();
            await using var crea = new NpgsqlCommand($"CREATE DATABASE \"{nomeDatabase}\"", admin);
            await crea.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<GestiSoftDbContext>()
                // Stessa strategia di retry dell'Api: senza, una transazione aperta nel modo sbagliato passa qui e fallisce in produzione.
                .UseNpgsql(new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = nomeDatabase }.ConnectionString, npgsql => npgsql.EnableRetryOnFailure())
                .Options;
            var credenziali = new CredenzialiProtector(new byte[32]);

            Guid strutturaId;
            await using (var db = new GestiSoftDbContext(options, credenziali))
            {
                await db.Database.MigrateAsync();
                strutturaId = await CreaStrutturaCompletaAsync(db);

                var vuote = await TabelleDellaStrutturaAsync(db, conteggio => conteggio == 0);
                Assert.True(vuote.Count == 0, $"Il test non popola queste tabelle: aggiungile qui e in EliminaStrutturaAsync — {string.Join(", ", vuote)}");
            }

            IReadOnlyList<string> foto;
            await using (var db = new GestiSoftDbContext(options, credenziali))
            {
                foto = await new SuperAdminRepository(db).EliminaStrutturaAsync(strutturaId, CancellationToken.None);
            }

            await using (var db = new GestiSoftDbContext(options, credenziali))
            {
                var rimaste = await TabelleDellaStrutturaAsync(db, conteggio => conteggio > 0);
                Assert.True(rimaste.Count == 0, $"Righe rimaste dopo l'eliminazione: {string.Join(", ", rimaste)}");
                Assert.Equal(1, await db.Clienti.CountAsync());
            }

            Assert.Equal(["struttura/foto-ancora-sul-disco.jpg"], foto);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(server.ConnectionString);
            await admin.OpenAsync();
            await using var elimina = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nomeDatabase}\" WITH (FORCE)", admin);
            await elimina.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Tabelle non globali per cui <paramref name="condizione"/> vale sul numero di righe. Il database
    /// è nuovo, quindi ogni riga di una tabella non globale è della Struttura di prova: si contano
    /// tutte, anche nelle tabelle ponte che non hanno uno StrutturaId proprio.
    /// </summary>
    private static async Task<List<string>> TabelleDellaStrutturaAsync(GestiSoftDbContext db, Func<int, bool> condizione)
    {
        var tabelle = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && !TabelleGlobali.Contains(t.ClrType) && t.GetTableName() is not null)
            .Select(t => t.GetTableName()!)
            .Distinct()
            .Order();

        var risultato = new List<string>();
        foreach (var tabella in tabelle)
        {
            // Il nome della tabella viene dal modello EF, non da un input: non si può passare come parametro.
            var sql = "SELECT COUNT(*)::int AS \"Value\" FROM \"" + tabella + "\"";
            var conteggio = await db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            if (condizione(conteggio))
            {
                risultato.Add(tabella);
            }
        }

        return risultato;
    }

    private static async Task<Guid> CreaStrutturaCompletaAsync(GestiSoftDbContext db)
    {
        var cliente = new Cliente { RagioneSociale = "Cliente di prova" };
        var utente = new Utente { Email = $"{Guid.NewGuid():N}@prova.invalid", ClienteId = cliente.Id };
        var struttura = new Struttura { ClienteId = cliente.Id, Nome = "Struttura di prova" };
        var s = struttura.Id;

        var tipologia = new SettingTipologia { StrutturaId = s, TipologiaCamera = "Doppia" };
        var camera = new SettingRoom { StrutturaId = s, Nome = "101", TipologiaId = tipologia.Id };
        var prenotazione = new Prenotazione { StrutturaId = s, CameraId = camera.Id, TipologiaId = tipologia.Id };
        var ospite = new Ospite { StrutturaId = s, PrenotazioneId = prenotazione.Id };
        var servizio = new ServizioStruttura { StrutturaId = s, Nome = "Parcheggio" };
        var fattura = new DatiFattura { StrutturaId = s, Progressivo = 1, NumeroDocumento = 1, DataDocumento = DateTime.UtcNow };
        var appartamento = new OsservatorioAppartamento { StrutturaId = s };
        var payTourist = new PayTouristIntegrazione { StrutturaId = s };
        var payTouristStruttura = new PayTouristStruttura { StrutturaId = s };
        var ticket = new Ticket { StrutturaId = s, Numero = Random.Shared.Next(1, int.MaxValue), Oggetto = "Prova" };
        var messaggio = new TicketMessaggio { StrutturaId = s, TicketId = ticket.Id, Testo = "Prova" };

        db.AddRange(cliente, utente, struttura, tipologia, camera, prenotazione, ospite, servizio, fattura, appartamento, payTourist, payTouristStruttura, ticket, messaggio);
        db.AddRange(
            new FasciaEtaSupplemento { StrutturaId = s, TipologiaId = tipologia.Id, EtaMax = 12 },
            new TrattamentoStruttura { StrutturaId = s },
            new PrenotazioneServizio { StrutturaId = s, PrenotazioneId = prenotazione.Id, ServizioId = servizio.Id, Nome = "Parcheggio" },
            new PagamentoPrenotazione { StrutturaId = s, PrenotazioneId = prenotazione.Id, Data = DateOnly.FromDateTime(DateTime.UtcNow), Importo = 10 },
            new RigaFattura { StrutturaId = s, DatiFatturaId = fattura.Id, Numero = 1, Descrizione = "Soggiorno" },
            new FatturaPrenotazione { StrutturaId = s, DatiFatturaId = fattura.Id, PrenotazioneId = prenotazione.Id },
            new GestionePrezzo { StrutturaId = s, TipologiaId = tipologia.Id },
            new SettingAgenzia { StrutturaId = s, Descrizione = "Diretta", Colore = "#000000" },
            new OspiteRiga { StrutturaId = s, OspiteId = ospite.Id, CameraId = camera.Id },
            new Spesa { StrutturaId = s },
            new Entrata { StrutturaId = s },
            new Cauzione { StrutturaId = s, PrenotazioneId = prenotazione.Id },
            new DatiAziendali { StrutturaId = s },
            new DatiCliente { StrutturaId = s },
            new UtenteStruttura { StrutturaId = s, UtenteId = utente.Id },
            new ImpostazioniStruttura { StrutturaId = s },
            new LogEvento { StrutturaId = s, Messaggio = "Prova", Origine = "Test" },
            new WubookIntegrazione { StrutturaId = s },
            new WubookEventoRicevuto { StrutturaId = s, Lcode = "1", Rcode = 1 },
            new RinnovoLicenza { StrutturaId = s, ScadenzaImpostata = DateTime.UtcNow },
            new AlloggiatiWebIntegrazione { StrutturaId = s },
            new OsservatorioAppartamentoTipologia { OsservatorioAppartamentoId = appartamento.Id, TipologiaId = tipologia.Id },
            new OsservatorioInvio { StrutturaId = s, PrenotazioneId = prenotazione.Id, StayId = "1", GuestId = "1", DataInvioUtc = DateTime.UtcNow },
            new PayTouristPortaleAttivo { PayTouristIntegrazioneId = payTourist.Id, IdPortale = 1, Nome = "Portale" },
            new PayTouristStrutturaTipologia { PayTouristStrutturaId = payTouristStruttura.Id, TipologiaId = tipologia.Id },
            new ChiusuraCamera { StrutturaId = s, CameraId = camera.Id, DataInizio = DateTime.UtcNow, DataFine = DateTime.UtcNow.AddDays(1) },
            new RestrizioneSoggiornoCamera { StrutturaId = s, CameraId = camera.Id, DataInizio = DateTime.UtcNow, DataFine = DateTime.UtcNow.AddDays(1) },
            new Notifica { StrutturaId = s, Titolo = "Prova", Messaggio = "Prova", PrenotazioneId = prenotazione.Id },
            new TicketAllegato { StrutturaId = s, MessaggioId = messaggio.Id, NomeFile = "a.jpg", ContentType = "image/jpeg", Percorso = "struttura/foto-ancora-sul-disco.jpg" },
            new TicketAllegato { StrutturaId = s, MessaggioId = messaggio.Id, NomeFile = "b.jpg", ContentType = "image/jpeg", Percorso = "struttura/foto-gia-cancellata.jpg", EliminatoAtUtc = DateTime.UtcNow });

        await db.SaveChangesAsync();
        return s;
    }
}
