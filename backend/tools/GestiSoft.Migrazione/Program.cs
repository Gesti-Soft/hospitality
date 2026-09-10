using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Migrazione one-shot di un'installazione legacy (SQL Server, 4 DB per-installazione) verso il
// nuovo gestionale multi-tenant (Postgres) — Fase 10. Pensato per essere rilanciato con altre
// connection string se in futuro arrivano altri clienti da migrare (nessuna logica specifica di
// QUESTO cliente è hardcoded oltre ai nomi di default delle connection string sorgente).
//
// Ordine di migrazione (rispetta le FK): Cliente/Struttura -> Tipologie -> Camere -> Prezzi ->
// Prenotazioni -> Ospiti -> Spese/Entrate -> DatiAziendali/DatiCliente -> Impostazioni ->
// Wubook/AlloggiatiWeb/Osservatorio/PayTourist.
//
// Deliberatamente NON migrato (vedi sessionreport.md per il dettaglio):
// - User/Permission (bcrypt legacy incompatibile con PasswordHasher<Utente> nuovo, nessuna Email
//   da mappare) — gli utenti reali vanno ricreati a mano dalla UI Utenti dopo la migrazione.
// - StatusRoom (chiusure camera manuali, feature non ancora costruita nel nuovo sistema).
// - Service/Logs (supervisione processi Windows sostituita da Docker; log tecnici legacy non
//   hanno un corrispettivo utile in LogEvento, che ha uno scopo diverso — solo errori 500).
// - SendStatus/CurrentData/StayId/SenToOsservatorio (stato interno del vecchio job in-memory:
//   verificato che SenToOsservatorio referenzia Id di Agenzie/Ospiti che non esistono più
//   nell'installazione corrente — dati orfani, riportarli rischierebbe di disallineare il cursore
//   Osservatorio invece di aiutare; si riparte pulito, ProssimoStayIdProgressivo=1).

var pgConnectionString = Environment.GetEnvironmentVariable("GESTISOFT_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=gestisoft;Username=gestisoft;Password=gestisoft_dev_only";

string SqlConn(string envVar, string defaultDb) =>
    Environment.GetEnvironmentVariable(envVar)
    ?? $"Server=.\\SQLEXPRESS;Database={defaultDb};Trusted_Connection=True;TrustServerCertificate=True";

var connAffittiCV = SqlConn("FASE10_SRC_AFFITTICV", "Fase10_Test_AffittiCV");
var connController = SqlConn("FASE10_SRC_CONTROLLER", "Fase10_Test_Controller");
var connOtaService = SqlConn("FASE10_SRC_OTASERVICE", "Fase10_Test_OtaService");
var connSyncStatePolice = SqlConn("FASE10_SRC_SYNCSTATEPOLICE", "Fase10_Test_SyncStatePolice");

var clienteNome = Environment.GetEnvironmentVariable("FASE10_CLIENTE_NOME") ?? "Gaetano Zummo";
var strutturaNome = Environment.GetEnvironmentVariable("FASE10_STRUTTURA_NOME") ?? "Villa Chifeci Scopello";

var optionsBuilder = new DbContextOptionsBuilder<GestiSoftDbContext>();
optionsBuilder.UseNpgsql(pgConnectionString);
await using var db = new GestiSoftDbContext(optionsBuilder.Options);

if (await db.Strutture.AnyAsync(s => s.Nome == strutturaNome))
{
    Console.WriteLine($"Una Struttura chiamata \"{strutturaNome}\" esiste già — migrazione già eseguita? Interrotto (nessuna scrittura).");
    return 1;
}

Console.WriteLine($"Migrazione verso Cliente \"{clienteNome}\" / Struttura \"{strutturaNome}\"...");

var cliente = new Cliente { RagioneSociale = clienteNome };
var struttura = new Struttura { Nome = strutturaNome, ClienteId = cliente.Id };
db.Clienti.Add(cliente);
db.Strutture.Add(struttura);
await db.SaveChangesAsync();
var strutturaId = struttura.Id;

// ---------------------------------------------------------------------------
// SettingTipology -> SettingTipologia
// ---------------------------------------------------------------------------
var tipologiaMap = new Dictionary<long, Guid>();
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, TipologiaCamera, SpesePulizia, Animali, Cauzione, DefoultPrezzo, Implemento, NImplementoPersona FROM SettingTipology", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var entity = new SettingTipologia
        {
            StrutturaId = strutturaId,
            TipologiaCamera = reader.GetString(1),
            SpesePulizia = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
            Animali = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            Cauzione = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
            PrezzoDefault = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            Implemento = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
            NumeroImplementoPersona = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
        };
        db.TipologieCamera.Add(entity);
        tipologiaMap[reader.GetInt64(0)] = entity.Id;
    }
}
await db.SaveChangesAsync();
Console.WriteLine($"Tipologie: {tipologiaMap.Count}");

// ---------------------------------------------------------------------------
// SettingRooms -> SettingRoom
// ---------------------------------------------------------------------------
var cameraMap = new Dictionary<long, Guid>();
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, StateRoom, Nome, CapacitaOspiti, SoggiornoMinimo, IdTipologia FROM SettingRooms", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var idTipologia = reader.IsDBNull(5) ? (long?)null : reader.GetInt64(5);
        var entity = new SettingRoom
        {
            StrutturaId = strutturaId,
            StateRoom = (StatoCamera)reader.GetInt32(1),
            Nome = reader.GetString(2),
            CapacitaOspiti = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            SoggiornoMinimo = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            TipologiaId = idTipologia is { } t && tipologiaMap.TryGetValue(t, out var tid) ? tid : null,
        };
        db.Camere.Add(entity);
        cameraMap[reader.GetInt64(0)] = entity.Id;
    }
}
await db.SaveChangesAsync();
Console.WriteLine($"Camere: {cameraMap.Count}");

// ---------------------------------------------------------------------------
// GestionePrezzi -> GestionePrezzo (IdAppartamento del legacy = IdCamera, nome storico)
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT id, IdAppartamento, DataInizio, DataFine, PrezzoPerNotte, IdTipologia FROM GestionePrezzi", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var idCamera = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
        var idTipologia = reader.IsDBNull(5) ? (long?)null : reader.GetInt64(5);
        db.PrezziCamera.Add(new GestionePrezzo
        {
            StrutturaId = strutturaId,
            CameraId = idCamera is { } c && cameraMap.TryGetValue(c, out var cid) ? cid : null,
            TipologiaId = idTipologia is { } t && tipologiaMap.TryGetValue(t, out var tid) ? tid : null,
            DataInizio = reader.IsDBNull(2) ? null : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
            DataFine = reader.IsDBNull(3) ? null : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
            PrezzoPerNotte = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Prezzi: {count}");
}

// ---------------------------------------------------------------------------
// SettingAgenzie -> SettingAgenzia
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Descrizione FROM SettingAgenzie", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        db.CanaliVendita.Add(new SettingAgenzia { StrutturaId = strutturaId, Descrizione = reader.GetString(0) });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Canali vendita: {count}");
}

// ---------------------------------------------------------------------------
// Agenzie -> Prenotazione
// ---------------------------------------------------------------------------
var prenotazioneMap = new Dictionary<long, Guid>();
DateTime? Utc(SqlDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand(
        "SELECT Id, IdCamera, Agenzia, NumeroPrenotazione, ImportoPrenotazione, ImportoPagato, CheckIn, CheckOut, NumeroOspiti, StatePolice, StatoPrenotazione, CheckEditSelection, Supply, ImportoTotale, Anno, PMS, PayTourist, TotalTax FROM Agenzie", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var idCamera = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
        var entity = new Prenotazione
        {
            StrutturaId = strutturaId,
            CameraId = idCamera is { } c && cameraMap.TryGetValue(c, out var cid) ? cid : null,
            Agenzia = reader.IsDBNull(2) ? null : reader.GetString(2),
            NumeroPrenotazione = reader.IsDBNull(3) ? null : reader.GetString(3),
            ImportoPrenotazione = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
            ImportoPagato = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            CheckIn = Utc(reader, 6),
            CheckOut = Utc(reader, 7),
            NumeroOspiti = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            StatePolice = reader.GetBoolean(9),
            StatoPrenotazione = reader.IsDBNull(10) ? null : (StatoPrenotazione)reader.GetInt32(10),
            CheckEditSelection = reader.IsDBNull(11) ? null : reader.GetString(11),
            Supply = reader.IsDBNull(12) ? null : reader.GetString(12),
            ImportoTotale = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
            Anno = reader.GetInt32(14),
            PMS = reader.GetBoolean(15),
            PayTourist = reader.GetBoolean(16),
            TotalTax = reader.IsDBNull(17) ? null : reader.GetDecimal(17),
        };
        db.Prenotazioni.Add(entity);
        prenotazioneMap[reader.GetInt64(0)] = entity.Id;
    }
}
await db.SaveChangesAsync();
Console.WriteLine($"Prenotazioni: {prenotazioneMap.Count}");

// ---------------------------------------------------------------------------
// Ospiti -> Ospite (typo LuoogoNascita->LuogoNascita, EsenteDaTAssa->EsenteDaTassa già corretti)
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand(
        "SELECT Id, TipoOspite, IdAgenzia, Permanenza, DataNascita, Sesso, Cognome, Nome, Cittadinanza, LuoogoNascita, StatoNascita, LuogoResidenza, Email, Documento, NumeroDocumento, RilascioDocumento, EsenteDaTAssa FROM Ospiti", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var idAgenzia = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2);
        db.Ospiti.Add(new Ospite
        {
            StrutturaId = strutturaId,
            TipoOspite = reader.IsDBNull(1) ? null : reader.GetString(1),
            PrenotazioneId = idAgenzia is { } a && prenotazioneMap.TryGetValue(a, out var pid) ? pid : null,
            Permanenza = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            DataNascita = Utc(reader, 4),
            Sesso = reader.IsDBNull(5) ? null : (Sesso)reader.GetInt32(5),
            Cognome = reader.IsDBNull(6) ? null : reader.GetString(6),
            Nome = reader.IsDBNull(7) ? null : reader.GetString(7),
            Cittadinanza = reader.IsDBNull(8) ? null : reader.GetString(8),
            LuogoNascita = reader.IsDBNull(9) ? null : reader.GetString(9),
            StatoNascita = reader.IsDBNull(10) ? null : reader.GetString(10),
            LuogoResidenza = reader.IsDBNull(11) ? null : reader.GetString(11),
            Email = reader.IsDBNull(12) ? null : reader.GetString(12),
            Documento = reader.IsDBNull(13) ? null : reader.GetString(13),
            NumeroDocumento = reader.IsDBNull(14) ? null : reader.GetString(14),
            RilascioDocumento = reader.IsDBNull(15) ? null : reader.GetString(15),
            EsenteDaTassa = reader.GetBoolean(16),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Ospiti: {count}");
}

// ---------------------------------------------------------------------------
// OspitiRow -> OspiteRiga (CameraOccupata testo libero non riportato, IdCamera è FK vera)
// ---------------------------------------------------------------------------
{
    // Serve la corrispondenza Ospiti.Id (legacy) -> Ospite.Id (nuovo) per risolvere IdOspite; la
    // ricostruiamo qui perché sopra non l'abbiamo tenuta (Ospite non referenzia altro per id legacy).
    var ospiteMap = new Dictionary<long, Guid>();
    await using (var conn = new SqlConnection(connAffittiCV))
    {
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Id, IdAgenzia FROM Ospiti", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var idsLegacy = new List<(long Id, long? IdAgenzia)>();
        while (await reader.ReadAsync())
        {
            idsLegacy.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1)));
        }
        // Rilegge gli Ospite appena inseriti nell'ordine di inserimento per abbinarli per IdAgenzia
        // (univoco: un solo Ospite per Prenotazione in questo schema).
        var ospitiInseriti = await db.Ospiti.Where(o => o.StrutturaId == strutturaId).ToListAsync();
        foreach (var (legacyId, idAgenzia) in idsLegacy)
        {
            var prenotazioneId = idAgenzia is { } a && prenotazioneMap.TryGetValue(a, out var pid) ? pid : (Guid?)null;
            var match = ospitiInseriti.FirstOrDefault(o => o.PrenotazioneId == prenotazioneId);
            if (match is not null)
            {
                ospiteMap[legacyId] = match.Id;
            }
        }
    }

    await using var connRow = new SqlConnection(connAffittiCV);
    await connRow.OpenAsync();
    await using var cmdRow = new SqlCommand(
        "SELECT IdOspite, IdCamera, Permanenza, DataNascita, Sesso, Cognome, Nome, Cittadinanza, LuoogoNascita, StatoNascita, LuogoResidenza, PostoLetto, EsenteDaTAssa FROM OspitiRow", connRow);
    await using var readerRow = await cmdRow.ExecuteReaderAsync();
    var count = 0;
    while (await readerRow.ReadAsync())
    {
        var idOspite = readerRow.IsDBNull(0) ? (long?)null : readerRow.GetInt64(0);
        var idCamera = readerRow.IsDBNull(1) ? (long?)null : readerRow.GetInt64(1);
        db.OspitiRighe.Add(new OspiteRiga
        {
            StrutturaId = strutturaId,
            OspiteId = idOspite is { } o && ospiteMap.TryGetValue(o, out var oid) ? oid : null,
            CameraId = idCamera is { } c && cameraMap.TryGetValue(c, out var cid) ? cid : null,
            Permanenza = readerRow.IsDBNull(2) ? null : readerRow.GetInt32(2),
            DataNascita = Utc(readerRow, 3),
            Sesso = readerRow.IsDBNull(4) ? null : (Sesso)readerRow.GetInt32(4),
            Cognome = readerRow.IsDBNull(5) ? null : readerRow.GetString(5),
            Nome = readerRow.IsDBNull(6) ? null : readerRow.GetString(6),
            Cittadinanza = readerRow.IsDBNull(7) ? null : readerRow.GetString(7),
            LuogoNascita = readerRow.IsDBNull(8) ? null : readerRow.GetString(8),
            StatoNascita = readerRow.IsDBNull(9) ? null : readerRow.GetString(9),
            LuogoResidenza = readerRow.IsDBNull(10) ? null : readerRow.GetString(10),
            PostoLetto = readerRow.IsDBNull(11) ? null : readerRow.GetBoolean(11),
            EsenteDaTassa = readerRow.GetBoolean(12),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Membri ospiti: {count}");
}

// ---------------------------------------------------------------------------
// Cauzioni -> Cauzione
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id_Prenotazione, ImportoCauzione, DataInserimento FROM Cauzioni", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var idPrenotazione = reader.GetInt64(0);
        if (!prenotazioneMap.TryGetValue(idPrenotazione, out var prenotazioneId))
        {
            Console.WriteLine($"  [skip] Cauzione con Id_Prenotazione={idPrenotazione} non trovato tra le prenotazioni migrate.");
            continue;
        }
        db.Cauzioni.Add(new Cauzione
        {
            StrutturaId = strutturaId,
            PrenotazioneId = prenotazioneId,
            ImportoCauzione = reader.IsDBNull(1) ? null : reader.GetDecimal(1),
            DataInserimento = Utc(reader, 2),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Cauzioni: {count}");
}

// ---------------------------------------------------------------------------
// Spese -> Spesa, Entrate -> Entrata
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT TipoSpesa, ImportoSpesa, Descrizione, MetodoPagamento, DataSpesa, Anno, Nome FROM Spese", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        db.Spese.Add(new Spesa
        {
            StrutturaId = strutturaId,
            TipoSpesa = reader.IsDBNull(0) ? null : reader.GetString(0),
            ImportoSpesa = reader.GetDecimal(1),
            Descrizione = reader.IsDBNull(2) ? null : reader.GetString(2),
            MetodoPagamento = reader.IsDBNull(3) ? null : reader.GetString(3),
            DataSpesa = Utc(reader, 4),
            Anno = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Spese: {count}");
}
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT TipoEntrata, Nome, ImportoEntrata, Descrizione, DataSpesa, Anno FROM Entrate", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        db.Entrate.Add(new Entrata
        {
            StrutturaId = strutturaId,
            TipoEntrata = reader.IsDBNull(0) ? null : reader.GetString(0),
            Nome = reader.IsDBNull(1) ? null : reader.GetString(1),
            ImportoEntrata = reader.GetDecimal(2),
            Descrizione = reader.IsDBNull(3) ? null : reader.GetString(3),
            Data = Utc(reader, 4),
            Anno = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Entrate: {count}");
}

// ---------------------------------------------------------------------------
// DatiAziendali, DatiCliente, DatiFattura
// ---------------------------------------------------------------------------
var datiClienteMap = new Dictionary<int, Guid>();
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Iso2, PIva, CodiceFiscale, Denominazione, Nome, Cognome, RegimeFiscale, Indirizzo, NCivico, Cap, Comune, Provincia, Nazione FROM DatiAziendali", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        db.DatiAziendali.Add(new DatiAziendali
        {
            StrutturaId = strutturaId,
            Iso2 = reader.IsDBNull(0) ? null : reader.GetString(0),
            PIva = reader.IsDBNull(1) ? null : reader.GetString(1),
            CodiceFiscale = reader.IsDBNull(2) ? null : reader.GetString(2),
            Denominazione = reader.IsDBNull(3) ? null : reader.GetString(3),
            Nome = reader.IsDBNull(4) ? null : reader.GetString(4),
            Cognome = reader.IsDBNull(5) ? null : reader.GetString(5),
            RegimeFiscale = reader.IsDBNull(6) ? null : (RegimeFiscale)reader.GetInt32(6),
            Indirizzo = reader.IsDBNull(7) ? null : reader.GetString(7),
            NCivico = reader.IsDBNull(8) ? null : reader.GetString(8),
            Cap = reader.IsDBNull(9) ? null : reader.GetString(9),
            Comune = reader.IsDBNull(10) ? null : reader.GetString(10),
            Provincia = reader.IsDBNull(11) ? null : reader.GetString(11),
            Nazione = reader.IsDBNull(12) ? null : reader.GetString(12),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Dati aziendali: {count}");
}
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, Iso2, PIva, CodiceFiscale, Denominazione, Nome, Cognome, Indirizzo, NCivico, Cap, Cittadinanza, Provincia, LuogoResidenza, CodiceDestinatario, Pec, CustomerKey FROM DatiCliente", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var entity = new DatiCliente
        {
            StrutturaId = strutturaId,
            Iso2 = reader.IsDBNull(1) ? null : reader.GetString(1),
            PIva = reader.IsDBNull(2) ? null : reader.GetString(2),
            CodiceFiscale = reader.IsDBNull(3) ? null : reader.GetString(3),
            Denominazione = reader.IsDBNull(4) ? null : reader.GetString(4),
            Nome = reader.IsDBNull(5) ? null : reader.GetString(5),
            Cognome = reader.IsDBNull(6) ? null : reader.GetString(6),
            Indirizzo = reader.IsDBNull(7) ? null : reader.GetString(7),
            NCivico = reader.IsDBNull(8) ? null : reader.GetString(8),
            Cap = reader.IsDBNull(9) ? null : reader.GetString(9),
            Cittadinanza = reader.IsDBNull(10) ? null : reader.GetString(10),
            Provincia = reader.IsDBNull(11) ? null : reader.GetString(11),
            LuogoResidenza = reader.IsDBNull(12) ? null : reader.GetString(12),
            CodiceDestinatario = reader.IsDBNull(13) ? null : reader.GetString(13),
            Pec = reader.IsDBNull(14) ? null : reader.GetString(14),
            CustomerKey = reader.IsDBNull(15) ? null : reader.GetString(15),
        };
        db.DatiCliente.Add(entity);
        datiClienteMap[reader.GetInt32(0)] = entity.Id;
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Dati cliente (fatturazione): {count}");
}
{
    await using var conn = new SqlConnection(connAffittiCV);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT IdCliente, TipoDocumento, RegimeFiscale, NumeroDocumento, DataDocumento, Divisa, Descrizione, Quantita, PrezzoUnitario, AliquotaIva, Natura, PrezzoTotale, ImportoTotale, Arrotondamento, Link, Progressivo, Anno FROM DatiFattura", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var idCliente = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
        db.DatiFattura.Add(new DatiFattura
        {
            StrutturaId = strutturaId,
            DatiClienteId = idCliente is { } c && datiClienteMap.TryGetValue(c, out var cid) ? cid : null,
            TipoDocumento = reader.IsDBNull(1) ? null : (TipoDocumentoFattura)reader.GetInt32(1),
            RegimeFiscale = reader.IsDBNull(2) ? null : (RegimeFiscale)reader.GetInt32(2),
            NumeroDocumento = reader.GetInt32(3),
            DataDocumento = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
            Divisa = reader.IsDBNull(5) ? null : reader.GetString(5),
            Descrizione = reader.IsDBNull(6) ? null : reader.GetString(6),
            Quantita = reader.GetDecimal(7),
            PrezzoUnitario = reader.GetDecimal(8),
            AliquotaIva = reader.IsDBNull(9) ? null : (AliquotaIva)reader.GetInt32(9),
            Natura = reader.IsDBNull(10) ? null : (NaturaIva)reader.GetInt32(10),
            PrezzoTotale = reader.GetDecimal(11),
            ImportoTotale = reader.GetDecimal(12),
            Arrotondamento = reader.GetDecimal(13),
            Link = reader.IsDBNull(14) ? null : reader.GetString(14),
            Progressivo = reader.GetInt32(15),
            Anno = reader.GetInt32(16),
        });
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Fatture: {count}");
}

// ---------------------------------------------------------------------------
// Controller.GeneralSettings -> ImpostazioniStruttura + PayTouristIntegrazione(parziale)
// ---------------------------------------------------------------------------
var settings = new Dictionary<string, string>();
{
    await using var conn = new SqlConnection(connController);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT [Key], Value FROM GeneralSettings", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        settings[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
    }
}

string? Get(string key) => settings.TryGetValue(key, out var v) ? v : null;
decimal? DecimalOrNull(string? s) => s is null ? null : decimal.Parse(s.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
int? IntOrNull(string? s) => s is null ? null : int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
bool Bool01(string? s) => s == "1";

var oraInvioStr = Get("ORA INVIO SCHEDINE");
db.ImpostazioniStruttura.Add(new ImpostazioniStruttura
{
    StrutturaId = strutturaId,
    PoliziaStatoAttiva = Bool01(Get("POLIZIA DI STATO (0 = NO, 1 = SI)")),
    OsservatorioAttivo = Bool01(Get("OSSERVATORIO TURISTICO (0 = NO, 1 = SI)")),
    PayTouristAttivo = Bool01(Get("PAYTOURIST (0 = NO, 1 = SI)")),
    OraInvioGiornaliero = oraInvioStr is null ? null : TimeOnly.Parse(oraInvioStr),
    TassaSoggiornoPrezzo = DecimalOrNull(Get("PREZZO TASSA DI SOGGIORNO")),
    TassaSoggiornoMaxGiorni = IntOrNull(Get("NUMERO GIORNI MAX")),
    ComuneAttivita = Get("COMUNE ATTIVITA'"),
});
Console.WriteLine("Impostazioni struttura: 1 (da GeneralSettings)");

var payTouristToken = Get("TOKEN PAYTOURIST");
db.PayTouristIntegrazioni.Add(new PayTouristIntegrazione
{
    StrutturaId = strutturaId,
    Token = payTouristToken,
    PortaleOnlineAttivo = Bool01(Get("PORTALE ONLINE (0 = NO, 1 = SI)")),
});
await db.SaveChangesAsync();
Console.WriteLine("Config PayTourist (token/portale): 1 (da GeneralSettings) — Id Software NON migrato, si legge sempre al volo da gestisoft.it.");

// ---------------------------------------------------------------------------
// Controller.Autentication -> WubookIntegrazione (licenza gestisoft.it)
// ---------------------------------------------------------------------------
var wubook = new WubookIntegrazione { StrutturaId = strutturaId };
{
    await using var conn = new SqlConnection(connController);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Username, Token FROM Autentication", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        wubook.GestisoftUsername = reader.IsDBNull(0) ? null : reader.GetString(0);
        wubook.GestisoftToken = reader.IsDBNull(1) ? null : reader.GetString(1);
    }
}

// ---------------------------------------------------------------------------
// OtaService.CamereAssociate -> WubookIntegrazione.Attivo + SettingTipologia.IdCameraWubook/WubookAttiva
// L'associazione OTA vive sulla Tipologia (il pool), non più sulla singola camera — se più camere
// legacy della stessa Tipologia avessero un IdCameraRemoto diverso (caso non atteso: nel legacy
// l'associazione era comunque 1 camera fisica = 1 camera Wubook, mai un pool), si segnala il
// conflitto invece di sovrascrivere silenziosamente.
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connOtaService);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT IdCameraLocale, IdCameraRemoto, Active FROM CamereAssociate", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var idLocale = reader.GetInt32(0);
        if (!cameraMap.TryGetValue(idLocale, out var cameraId))
        {
            Console.WriteLine($"  [skip] CamereAssociate.IdCameraLocale={idLocale} non trovato tra le camere migrate.");
            continue;
        }

        var camera = await db.Camere.FirstAsync(c => c.Id == cameraId);
        if (camera.TipologiaId is not { } tipologiaId)
        {
            Console.WriteLine($"  [skip] Camera {cameraId} (legacy id {idLocale}) non ha una Tipologia: impossibile associarla a OTA.");
            continue;
        }

        var tipologia = await db.TipologieCamera.FirstAsync(t => t.Id == tipologiaId);
        var idRemoto = reader.GetInt32(1);
        var attiva = reader.GetBoolean(2);
        if (tipologia.WubookAttiva && tipologia.IdCameraWubook is { } giaAssociato && giaAssociato != idRemoto)
        {
            Console.WriteLine($"  [conflitto] Tipologia '{tipologia.TipologiaCamera}' già associata a IdCameraWubook={giaAssociato}, CamereAssociate propone {idRemoto} (camera legacy {idLocale}) — non sovrascritto, verificare manualmente.");
            continue;
        }

        tipologia.IdCameraWubook = idRemoto;
        tipologia.WubookAttiva = attiva;
        count++;
    }
    if (count > 0)
    {
        wubook.Attivo = true;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Associazioni camere Wubook (per Tipologia): {count}");
}
db.WubookIntegrazioni.Add(wubook);
await db.SaveChangesAsync();
Console.WriteLine("Licenza gestisoft.it/Wubook: 1 (da Autentication)");

// ---------------------------------------------------------------------------
// SyncStatePolice.Settings -> AlloggiatiWebIntegrazione
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connSyncStatePolice);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Utente, WebKey, Password FROM Settings", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        db.AlloggiatiWebIntegrazioni.Add(new AlloggiatiWebIntegrazione
        {
            StrutturaId = strutturaId,
            Utente = reader.IsDBNull(0) ? null : reader.GetString(0),
            WsKey = reader.IsDBNull(1) ? null : reader.GetString(1),
            Password = reader.IsDBNull(2) ? null : reader.GetString(2),
        });
        await db.SaveChangesAsync();
        Console.WriteLine("Credenziali Alloggiati Web: 1 (da Settings)");
    }
}

// ---------------------------------------------------------------------------
// SyncStatePolice.Appartamenti -> OsservatorioAppartamento (+ tutte le Tipologie collegate,
// nessun campo legacy distingue quali tipologie vadano a quale appartamento in questo schema)
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connSyncStatePolice);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT EntityCode, HotelCode, Password FROM Appartamenti", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    while (await reader.ReadAsync())
    {
        var appartamento = new OsservatorioAppartamento
        {
            StrutturaId = strutturaId,
            Nome = strutturaNome,
            EntityCode = reader.IsDBNull(0) ? null : reader.GetString(0),
            HotelCode = reader.IsDBNull(1) ? null : reader.GetString(1),
            Password = reader.IsDBNull(2) ? null : reader.GetString(2),
        };
        foreach (var tipologiaId in tipologiaMap.Values)
        {
            appartamento.Tipologie.Add(new OsservatorioAppartamentoTipologia { OsservatorioAppartamentoId = appartamento.Id, TipologiaId = tipologiaId });
        }
        db.OsservatorioAppartamenti.Add(appartamento);
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Appartamenti Osservatorio: {count} (tutte le tipologie collegate a ciascuno, nessuna routing granulare nel legacy)");
}

// ---------------------------------------------------------------------------
// SyncStatePolice.PayTouristUser -> PayTouristStruttura (+ tipologia collegata se risolvibile)
// ---------------------------------------------------------------------------
{
    await using var conn = new SqlConnection(connSyncStatePolice);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT IdStructure, Utente, IdTipology FROM PayTouristUser", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var count = 0;
    var tipologieNonRisolte = 0;
    while (await reader.ReadAsync())
    {
        var payTouristStruttura = new PayTouristStruttura
        {
            StrutturaId = strutturaId,
            Nome = reader.IsDBNull(1) ? null : reader.GetString(1),
            IdStrutturaPaytourist = reader.IsDBNull(0) ? null : reader.GetInt32(0),
        };
        if (!reader.IsDBNull(2) && long.TryParse(reader.GetString(2), out var idTipologiaLegacy) && tipologiaMap.TryGetValue(idTipologiaLegacy, out var tipologiaId))
        {
            payTouristStruttura.Tipologie.Add(new PayTouristStrutturaTipologia { PayTouristStrutturaId = payTouristStruttura.Id, TipologiaId = tipologiaId });
        }
        else
        {
            tipologieNonRisolte++;
        }
        db.PayTouristStrutture.Add(payTouristStruttura);
        count++;
    }
    await db.SaveChangesAsync();
    Console.WriteLine($"Strutture PayTourist: {count}" + (tipologieNonRisolte > 0 ? $" ({tipologieNonRisolte} con IdTipology legacy non risolvibile — struttura migrata comunque, senza tipologia collegata, da assegnare a mano)" : ""));
}

Console.WriteLine();
Console.WriteLine("Migrazione completata.");
Console.WriteLine($"ClienteId: {cliente.Id}");
Console.WriteLine($"StrutturaId: {strutturaId}");
return 0;
