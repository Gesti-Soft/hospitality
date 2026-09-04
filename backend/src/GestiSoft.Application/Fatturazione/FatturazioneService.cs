using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

public record CreaFatturaDaPrenotazioneRequest(
    Guid PrenotazioneId,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    string? Descrizione,
    decimal Quantita,
    decimal? PrezzoUnitario,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    string? Divisa);

public record AggiornaFatturaRequest(
    Guid? DatiClienteId,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    string? Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    string? Divisa);

/// <summary>
/// Fatture — porta FatturazioneViewModel del legacy (Sezione C del report Fase 4). La fattura
/// nasce sempre da una Prenotazione: il destinatario è l'Ospite capofila della scheda alloggiati
/// collegata, deduplicato tramite CustomerKey (NumeroDocumento-Nome-Cognome-DataNascita, stessa
/// formula del legacy). Il calcolo automatico del Codice Fiscale dall'anagrafica ospite (che il
/// legacy faceva con un generatore CF + tabella Belfiore) NON è stato portato — l'operatore lo
/// completa a mano sul DatiCliente, TODO per una fase dedicata se serve.
/// A differenza del legacy (dove Progressivo non veniva mai valorizzato, vedi report Sez. E.1-2),
/// qui è un vero contatore per (StrutturaId, Anno), con retry su conflitto di concorrenza.
/// </summary>
public class FatturazioneService(
    IDatiFatturaRepository fatture,
    IDatiClienteRepository clienti,
    IDatiAziendaliRepository aziende,
    IPrenotazioneRepository prenotazioni,
    IOspiteRepository ospiti,
    IFatturaDocumentGenerator documentGenerator,
    PermessoStrutturaGuard permessoGuard)
{
    private const int MassimiTentativiProgressivo = 5;

    public async Task<IReadOnlyList<DatiFattura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await fatture.ListAsync(strutturaId, anno, cancellationToken);
    }

    public async Task<DatiFattura> GetAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
    }

    /// <summary>L'eventuale fattura già generata per questa Prenotazione — usata dalla scheda ospiti per mostrare "Fattura generata" invece di lasciarlo scoprire solo aprendo Fatturazione.</summary>
    public async Task<DatiFattura?> GetByPrenotazioneAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        var fattura = await fatture.GetByPrenotazioneIdAsync(prenotazioneId, cancellationToken);
        return fattura is { } f && f.StrutturaId == strutturaId ? f : null;
    }

    public async Task<DatiFattura> CreaDaPrenotazioneAsync(ICurrentUser currentUser, Guid strutturaId, CreaFatturaDaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(request.PrenotazioneId, cancellationToken)
            ?? throw new NotFoundException("Prenotazione non trovata.");
        if (prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        var capofila = await ospiti.GetByPrenotazioneAsync(prenotazione.Id, cancellationToken)
            ?? throw new ConflictException("La prenotazione non ha ancora una scheda ospiti: compilala prima di fatturare.");

        var (cliente, _) = await RisolviOTrovaClienteAsync(strutturaId, capofila, cancellationToken);

        var prezzoUnitario = request.PrezzoUnitario ?? prenotazione.ImportoTotale ?? 0;
        var prezzoTotale = request.Quantita * prezzoUnitario;
        var aliquotaPercentuale = request.AliquotaIva is { } aliquota ? (decimal)aliquota / 100m : 0m;
        var importoTotale = Math.Round(prezzoTotale * (1 + aliquotaPercentuale), 2, MidpointRounding.AwayFromZero);

        var anno = DateTime.UtcNow.Year;
        for (var tentativo = 0; tentativo < MassimiTentativiProgressivo; tentativo++)
        {
            var progressivo = await fatture.GetMaxProgressivoAsync(strutturaId, anno, cancellationToken) + 1;

            var candidata = new DatiFattura
            {
                StrutturaId = strutturaId,
                PrenotazioneId = prenotazione.Id,
                DatiClienteId = cliente.Id,
                Progressivo = progressivo,
                NumeroDocumento = progressivo,
                TipoDocumento = request.TipoDocumento,
                RegimeFiscale = request.RegimeFiscale,
                DataDocumento = DateTime.UtcNow.Date,
                Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? "EUR" : request.Divisa,
                Descrizione = request.Descrizione,
                Quantita = request.Quantita,
                PrezzoUnitario = prezzoUnitario,
                PrezzoTotale = prezzoTotale,
                ImportoTotale = importoTotale,
                AliquotaIva = request.AliquotaIva,
                Natura = request.Natura,
                Anno = anno,
            };

            if (await fatture.TryAddAsync(candidata, cancellationToken))
            {
                return candidata;
            }
        }

        throw new ConflictException("Impossibile generare il numero fattura, riprova.");
    }

    public async Task<DatiFattura> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, AggiornaFatturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);

        if (request.DatiClienteId is { } clienteId)
        {
            var cliente = await clienti.GetAsync(clienteId, cancellationToken) ?? throw new NotFoundException("Cliente non trovato.");
            if (cliente.StrutturaId != strutturaId)
            {
                throw new NotFoundException("Cliente non trovato.");
            }
        }

        var prezzoTotale = request.Quantita * request.PrezzoUnitario;
        var aliquotaPercentuale = request.AliquotaIva is { } aliquota ? (decimal)aliquota / 100m : 0m;

        entity.DatiClienteId = request.DatiClienteId;
        entity.TipoDocumento = request.TipoDocumento;
        entity.RegimeFiscale = request.RegimeFiscale;
        entity.Descrizione = request.Descrizione;
        entity.Quantita = request.Quantita;
        entity.PrezzoUnitario = request.PrezzoUnitario;
        entity.PrezzoTotale = prezzoTotale;
        entity.ImportoTotale = Math.Round(prezzoTotale * (1 + aliquotaPercentuale), 2, MidpointRounding.AwayFromZero);
        entity.AliquotaIva = request.AliquotaIva;
        entity.Natura = request.Natura;
        entity.Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? entity.Divisa : request.Divisa;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await fatture.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<byte[]> GeneraPdfAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var (fattura, cliente, azienda) = await CaricaPerDocumentoAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return documentGenerator.GeneraPdf(fattura, cliente, azienda);
    }

    public async Task<byte[]> GeneraXmlSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var (fattura, cliente, azienda) = await CaricaPerDocumentoAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        return documentGenerator.GeneraXmlSdi(fattura, cliente, azienda);
    }

    private async Task<(DatiFattura Fattura, DatiCliente? Cliente, DatiAziendali? Azienda)> CaricaPerDocumentoAsync(
        ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var fattura = await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
        var cliente = fattura.DatiClienteId is { } clienteId ? await clienti.GetAsync(clienteId, cancellationToken) : null;
        var azienda = await aziende.GetByStrutturaIdAsync(strutturaId, cancellationToken);

        return (fattura, cliente, azienda);
    }

    /// <summary>
    /// Trova o crea il Cliente fatturabile per l'ospite capofila di una Prenotazione — usata sia alla
    /// creazione reale di una fattura, sia per una risoluzione "anticipata" (<see cref="RisolviClientePerPrenotazioneAsync"/>)
    /// che permette all'operatore di completarlo (P.IVA/CF/indirizzo/PEC) PRIMA di generare la fattura,
    /// invece di scoprire solo alla fine che ne è stato creato uno bare-bones. CustomerKey =
    /// NumeroDocumento-Nome-Cognome-DataNascita, stessa formula del legacy per deduplicare i clienti —
    /// mai risolto due volte in un Cliente diverso per lo stesso ospite.
    /// </summary>
    private async Task<(DatiCliente Cliente, bool AppenaCreato)> RisolviOTrovaClienteAsync(Guid strutturaId, Ospite capofila, CancellationToken cancellationToken)
    {
        var customerKey = $"{capofila.NumeroDocumento}-{capofila.Nome}-{capofila.Cognome}-{capofila.DataNascita:yyyyMMdd}";

        var esistente = await clienti.GetByCustomerKeyAsync(strutturaId, customerKey, cancellationToken);
        if (esistente is not null)
        {
            return (esistente, false);
        }

        var nuovo = new DatiCliente
        {
            StrutturaId = strutturaId,
            Nome = capofila.Nome,
            Cognome = capofila.Cognome,
            LuogoResidenza = capofila.LuogoResidenza,
            Cittadinanza = capofila.Cittadinanza,
            CustomerKey = customerKey,
        };

        await clienti.AddAsync(nuovo, cancellationToken);
        return (nuovo, true);
    }

    /// <summary>
    /// Risolve (senza ancora fatturare nulla) il Cliente fatturabile per la prenotazione scelta nel
    /// dialog "Nuova fattura" — se non esisteva ancora, ne crea subito uno bare-bones (nome/cognome/
    /// residenza/cittadinanza dalla scheda ospiti) e lo segnala come "appena creato", così il frontend
    /// può aprire immediatamente il form per completarlo (P.IVA/CF/indirizzo/PEC) prima che l'operatore
    /// prosegua con "Crea fattura" — mai un secondo Cliente duplicato più tardi, è lo stesso identico
    /// record (stessa CustomerKey) che CreaDaPrenotazioneAsync userebbe comunque.
    /// </summary>
    public async Task<(DatiCliente Cliente, bool AppenaCreato)> RisolviClientePerPrenotazioneAsync(
        ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken)
            ?? throw new NotFoundException("Prenotazione non trovata.");
        if (prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        var capofila = await ospiti.GetByPrenotazioneAsync(prenotazione.Id, cancellationToken)
            ?? throw new ConflictException("La prenotazione non ha ancora una scheda ospiti: compilala prima di fatturare.");

        return await RisolviOTrovaClienteAsync(strutturaId, capofila, cancellationToken);
    }

    private async Task<DatiFattura> GetOwnedAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var entity = await fatture.GetAsync(fatturaId, cancellationToken) ?? throw new NotFoundException("Fattura non trovata.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Fattura non trovata.");
        }

        return entity;
    }
}
