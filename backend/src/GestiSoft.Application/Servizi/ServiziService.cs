using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Fatturazione;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Contracts.Servizi;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Servizi;

/// <summary>
/// Servizi extra della struttura (escursioni, parcheggio, transfer…) e quelli venduti con le
/// prenotazioni. Si sommano al prezzo del soggiorno come i trattamenti, ma una prenotazione può
/// averne più d'uno. Come i trattamenti, la prenotazione copia il prezzo del momento e non va all'OTA.
/// </summary>
public class ServiziService(
    IServizioStrutturaRepository servizi,
    IPrenotazioneRepository prenotazioni,
    PermessoStrutturaGuard permessoGuard,
    TenantAccessGuard accessGuard,
    GestioneUtentiGuard gestioneUtentiGuard,
    ILogEventoService logEventi)
{
    public const int LunghezzaMassimaNome = 100;
    public const int QuantitaMassima = 99;

    /// <summary>Lo legge anche chi fa le prenotazioni, per scegliere i servizi: non solo chi imposta i prezzi.</summary>
    public async Task<IReadOnlyList<ServizioStruttura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingRoomRead || p.ReservationRead, cancellationToken);
        return await servizi.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Stesso permesso dei trattamenti e delle altre impostazioni generali: è una decisione del titolare.</summary>
    public async Task<ServizioStruttura> CreaAsync(ICurrentUser currentUser, Guid strutturaId, SalvaServizioRequest request, CancellationToken cancellationToken)
    {
        await EnsurePuoModificareAsync(currentUser, strutturaId, cancellationToken);
        var nome = await ValidaAsync(strutturaId, servizioId: null, request, cancellationToken);

        var entity = new ServizioStruttura { StrutturaId = strutturaId };
        Copia(entity, nome, request);
        await servizi.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<ServizioStruttura> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid servizioId, SalvaServizioRequest request, CancellationToken cancellationToken)
    {
        await EnsurePuoModificareAsync(currentUser, strutturaId, cancellationToken);
        var entity = await GetNonEliminatoAsync(strutturaId, servizioId, cancellationToken);
        var nome = await ValidaAsync(strutturaId, servizioId, request, cancellationToken);

        Copia(entity, nome, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await servizi.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Lo toglie dal listino e dalle scelte, ma non dal database: le prenotazioni che lo hanno venduto
    /// devono continuare a mostrarlo e a contarlo nel conto.
    /// </summary>
    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid servizioId, CancellationToken cancellationToken)
    {
        await EnsurePuoModificareAsync(currentUser, strutturaId, cancellationToken);
        var entity = await GetNonEliminatoAsync(strutturaId, servizioId, cancellationToken);

        entity.Eliminato = true;
        entity.Attivo = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await servizi.UpsertAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<PrenotazioneServizio>> ListaDellaPrenotazioneAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationRead, cancellationToken);
        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        return await servizi.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
    }

    public Task<IReadOnlyList<PrenotazioneServizio>> ListaDellaPrenotazioneSistemaAsync(Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken) =>
        servizi.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);

    /// <summary>
    /// Righe che la prenotazione avrà con i servizi richiesti, non ancora salvate. Una riga che la
    /// prenotazione ha già (RigaId) tiene nome, prezzo, origine e autore con cui è stata venduta, anche
    /// se nel frattempo il servizio è stato eliminato o non è più offerto: cambiano solo quantità e date.
    /// Una nuova prende il prezzo del listino, e il servizio deve essere offerto.
    /// Le date stanno dentro il soggiorno: un servizio dall'arrivo alla partenza compresa, quelli a notte
    /// solo nelle notti del soggiorno.
    /// </summary>
    public async Task<List<PrenotazioneServizio>> RisolviRigheAsync(
        Guid strutturaId,
        Guid? prenotazioneId,
        IReadOnlyList<ServizioPrenotazioneRichiesta> richieste,
        DateTime checkIn,
        DateTime checkOut,
        OrigineServizio origineNuove,
        string? aggiuntoDa,
        CancellationToken cancellationToken)
    {
        var righeId = richieste.Where(r => r.RigaId is not null).Select(r => r.RigaId).ToList();
        if (righeId.Distinct().Count() != righeId.Count)
        {
            throw new ConflictException("La stessa riga di servizio compare due volte.");
        }

        var esistenti = prenotazioneId is { } id
            ? await servizi.ListByPrenotazioneAsync(strutturaId, id, cancellationToken)
            : [];
        var arrivo = DateOnly.FromDateTime(checkIn);
        var partenza = DateOnly.FromDateTime(checkOut);

        var righe = new List<PrenotazioneServizio>();
        foreach (var richiesta in richieste)
        {
            if (richiesta.Quantita is < 1 or > QuantitaMassima)
            {
                throw new ConflictException($"La quantità di un servizio va da 1 a {QuantitaMassima}.");
            }

            PrenotazioneServizio riga;
            if (richiesta.RigaId is { } rigaId)
            {
                var esistente = esistenti.FirstOrDefault(e => e.Id == rigaId && e.ServizioId == richiesta.ServizioId)
                    ?? throw new NotFoundException("Servizio della prenotazione non trovato: ricarica la pagina.");
                riga = new PrenotazioneServizio
                {
                    Id = esistente.Id,
                    StrutturaId = strutturaId,
                    PrenotazioneId = esistente.PrenotazioneId,
                    ServizioId = esistente.ServizioId,
                    Nome = esistente.Nome,
                    Modalita = esistente.Modalita,
                    PrezzoUnitario = esistente.PrezzoUnitario,
                    Origine = esistente.Origine,
                    AggiuntoDa = esistente.AggiuntoDa,
                    CreatedAtUtc = esistente.CreatedAtUtc,
                };
            }
            else
            {
                var servizio = await servizi.GetAsync(strutturaId, richiesta.ServizioId, cancellationToken)
                    ?? throw new NotFoundException("Servizio non trovato.");
                if (servizio.Eliminato || !servizio.Attivo)
                {
                    throw new ConflictException($"Il servizio \"{servizio.Nome}\" non è offerto: attivalo in Impostazioni, scheda Servizi.");
                }

                riga = new PrenotazioneServizio
                {
                    StrutturaId = strutturaId,
                    PrenotazioneId = prenotazioneId ?? Guid.Empty,
                    ServizioId = servizio.Id,
                    Nome = servizio.Nome,
                    Modalita = servizio.Modalita,
                    PrezzoUnitario = servizio.Prezzo,
                    Origine = origineNuove,
                    AggiuntoDa = aggiuntoDa,
                };
            }

            riga.Quantita = richiesta.Quantita;
            (riga.Dal, riga.Al) = ValidaDate(riga.Nome, riga.Modalita, richiesta.Dal, richiesta.Al, arrivo, partenza);
            righe.Add(riga);
        }

        return righe;
    }

    /// <summary>
    /// A notte: dal–al dentro il soggiorno, almeno una notte (Al come il check-out, escluso). Gli altri:
    /// un giorno solo, dall'arrivo alla partenza compresa (un transfer si fa anche il giorno della partenza).
    /// </summary>
    public static (DateOnly Dal, DateOnly? Al) ValidaDate(string nome, ModalitaPrezzoServizio modalita, DateOnly dal, DateOnly? al, DateOnly arrivo, DateOnly partenza)
    {
        if (PerNotte(modalita))
        {
            if (al is not { } fine || dal < arrivo || fine > partenza || fine <= dal)
            {
                throw new ConflictException($"Le notti di \"{nome}\" devono stare dentro il soggiorno ({arrivo:dd/MM}–{partenza:dd/MM}), almeno una.");
            }

            return (dal, fine);
        }

        if (dal < arrivo || dal > partenza)
        {
            throw new ConflictException($"La data di \"{nome}\" deve stare dentro il soggiorno, dal {arrivo:dd/MM} al {partenza:dd/MM}.");
        }

        return (dal, null);
    }

    public static bool PerNotte(ModalitaPrezzoServizio modalita) =>
        modalita is ModalitaPrezzoServizio.APersonaANotte or ModalitaPrezzoServizio.ANotte;

    /// <summary>Dopo il check-in (e dopo il check-out) quello che si aggiunge è un addebito sul conto, non una vendita con la prenotazione.</summary>
    public static OrigineServizio OrigineNuove(StatoPrenotazione? stato) =>
        stato is StatoPrenotazione.InCorso or StatoPrenotazione.Completata ? OrigineServizio.DuranteIlSoggiorno : OrigineServizio.ConLaPrenotazione;

    /// <summary>
    /// Addebito rapido dalla reception (pagina Check-in/out), senza aprire la prenotazione: aggiunge la
    /// riga e ne somma l'importo al totale della prenotazione, perché è un addebito deciso in quel
    /// momento. Dal dialogo della prenotazione invece il nuovo totale si propone e lo conferma l'operatore.
    /// </summary>
    public async Task<PrenotazioneServizio> AddebitaAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        Guid prenotazioneId,
        ServizioPrenotazioneRichiesta richiesta,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.ReservationWrite, cancellationToken);
        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        if (prenotazione.StatoPrenotazione == StatoPrenotazione.Annullata || prenotazione is not { CheckIn: { } arrivo, CheckOut: { } partenza })
        {
            throw new ConflictException("Su una prenotazione annullata non si addebita nulla.");
        }

        var esistenti = await servizi.ListByPrenotazioneAsync(strutturaId, prenotazioneId, cancellationToken);
        var richieste = esistenti
            .Select(e => new ServizioPrenotazioneRichiesta(e.Id, e.ServizioId, e.Quantita, e.Dal, e.Al))
            .Append(richiesta with { RigaId = null })
            .ToList();
        var righe = await RisolviRigheAsync(
            strutturaId, prenotazioneId, richieste, arrivo, partenza, OrigineNuove(prenotazione.StatoPrenotazione), currentUser.Email, cancellationToken);
        var nuova = righe[^1];

        await SalvaRigheAsync(strutturaId, prenotazioneId, righe, cancellationToken);

        var importo = Importo(nuova);
        prenotazione.ImportoTotale = (prenotazione.ImportoTotale ?? 0) + importo;
        prenotazione.UpdatedAtUtc = DateTime.UtcNow;
        await prenotazioni.UpdateAsync(prenotazione, cancellationToken);

        await logEventi.RegistraAsync(
            LivelloLog.Info,
            $"Addebitato sulla prenotazione #{prenotazione.NumeroPrenotazione ?? prenotazione.Id.ToString()[..8]}: {Descrivi([nuova])}, {importo:0.00}€ (importo totale ora {prenotazione.ImportoTotale:0.00}€).",
            origine: "Api",
            clienteId: currentUser.ClienteId,
            strutturaId: strutturaId,
            categoria: "Prenotazione",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);
        return nuova;
    }

    public Task SalvaRigheAsync(Guid strutturaId, Guid prenotazioneId, List<PrenotazioneServizio> righe, CancellationToken cancellationToken)
    {
        foreach (var riga in righe)
        {
            riga.PrenotazioneId = prenotazioneId;
        }

        return servizi.SostituisciDellaPrenotazioneAsync(strutturaId, prenotazioneId, righe, cancellationToken);
    }

    /// <summary>Importo di una riga: prezzo × quantità, e × notti per i servizi "a notte".</summary>
    public static decimal Importo(ModalitaPrezzoServizio modalita, decimal prezzoUnitario, int quantita, int notti) =>
        prezzoUnitario * quantita * (PerNotte(modalita) ? notti : 1);

    /// <summary>Con le notti della riga, non di tutto il soggiorno: il parcheggio preso per 3 notti su 5 conta 3.</summary>
    public static decimal Importo(PrenotazioneServizio riga) =>
        Importo(riga.Modalita, riga.PrezzoUnitario, riga.Quantita, riga.Al is { } al ? al.DayNumber - riga.Dal.DayNumber : 0);

    /// <summary>Per il log delle modifiche economiche: "SPA 12/10 x2, Parcheggio 12/10–15/10 x1".</summary>
    public static string Descrivi(IEnumerable<PrenotazioneServizio> righe)
    {
        var testo = string.Join(", ", righe
            .OrderBy(r => r.Dal)
            .Select(r => $"{r.Nome} {r.Dal:dd/MM}{(r.Al is { } al ? $"–{al:dd/MM}" : "")} x{r.Quantita}"));
        return testo == "" ? "nessuno" : testo;
    }

    private async Task EnsurePuoModificareAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(currentUser, strutturaId, cancellationToken);
        await gestioneUtentiGuard.EnsureAsync(currentUser, strutturaId, cancellationToken);
    }

    private async Task<ServizioStruttura> GetNonEliminatoAsync(Guid strutturaId, Guid servizioId, CancellationToken cancellationToken)
    {
        var entity = await servizi.GetAsync(strutturaId, servizioId, cancellationToken);
        return entity is { Eliminato: false } ? entity : throw new NotFoundException("Servizio non trovato.");
    }

    private async Task<string> ValidaAsync(Guid strutturaId, Guid? servizioId, SalvaServizioRequest request, CancellationToken cancellationToken)
    {
        var nome = request.Nome?.Trim() ?? "";
        if (nome == "")
        {
            throw new ConflictException("Indica il nome del servizio.");
        }

        if (nome.Length > LunghezzaMassimaNome)
        {
            throw new ConflictException($"Il nome del servizio può avere al massimo {LunghezzaMassimaNome} caratteri.");
        }

        if (!Enum.IsDefined(request.Modalita))
        {
            throw new ConflictException("Modalità di prezzo non riconosciuta.");
        }

        if (request.Prezzo < 0)
        {
            throw new ConflictException("Il prezzo di un servizio non può essere negativo.");
        }

        if (request.AliquotaIva is { } aliquota && !Enum.IsDefined(aliquota) || request.Natura is { } natura && !Enum.IsDefined(natura))
        {
            throw new ConflictException("Aliquota IVA non riconosciuta.");
        }

        // Entrambe vuote = quella della struttura; altrimenti la stessa regola delle righe di fattura.
        if (request.AliquotaIva is not null || request.Natura is not null)
        {
            CalcoloFattura.AliquotaENatura(request.AliquotaIva, request.Natura, $"Servizio \"{nome}\"");
        }

        var altri = await servizi.ListByStrutturaAsync(strutturaId, cancellationToken);
        if (altri.Any(s => s.Id != servizioId && string.Equals(s.Nome, nome, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"Esiste già un servizio \"{nome}\".");
        }

        return nome;
    }

    private static void Copia(ServizioStruttura entity, string nome, SalvaServizioRequest request)
    {
        entity.Nome = nome;
        entity.Prezzo = request.Prezzo;
        entity.Modalita = request.Modalita;
        entity.Attivo = request.Attivo;
        entity.AliquotaIva = request.AliquotaIva;
        entity.Natura = request.Natura;
    }
}
