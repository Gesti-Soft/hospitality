using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Riferimenti;
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
    IRiferimentiRepository riferimenti,
    IStrutturaRepository strutture,
    IFatturaDocumentGenerator documentGenerator,
    PermessoStrutturaGuard permessoGuard)
{
    private const int MassimiTentativiProgressivo = 5;

    public async Task<IReadOnlyList<DatiFattura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await fatture.ListAsync(strutturaId, anno, cancellationToken);
    }

    /// <summary>Anni con almeno una fattura emessa — per il selettore Anno.</summary>
    public async Task<IReadOnlyList<int>> GetAnniDisponibiliAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await fatture.ListaAnniConDatiAsync(strutturaId, cancellationToken);
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
        EsigiDescrizione(request.Descrizione);

        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var prenotazione = await prenotazioni.GetAsync(request.PrenotazioneId, cancellationToken)
            ?? throw new NotFoundException("Prenotazione non trovata.");
        if (prenotazione.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Prenotazione non trovata.");
        }

        var capofila = await ospiti.GetByPrenotazioneAsync(prenotazione.Id, cancellationToken)
            ?? throw new ConflictException("La prenotazione non ha ancora una scheda ospiti: compilala prima di fatturare.");

        var (cliente, _) = await RisolviOCreaClienteAsync(strutturaId, capofila, cancellationToken);

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
        EsigiDescrizione(request.Descrizione);

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
        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
        return documentGenerator.GeneraPdf(fattura, cliente, azienda, struttura?.Nome);
    }

    public async Task<byte[]> GeneraXmlSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var (fattura, cliente, azienda) = await CaricaPerDocumentoAsync(currentUser, strutturaId, fatturaId, cancellationToken);

        // Un dato obbligatorio mancante diventerebbe un elemento vuoto e lo SDI scarterebbe il file
        // giorni dopo, quando nessuno ricorda più quella fattura: meglio non generarlo e dire cosa
        // manca. Il PDF resta scaricabile comunque, non ha vincoli di tracciato.
        var motivi = documentGenerator.ValidaPerSdi(fattura, cliente, azienda);
        if (motivi.Count > 0)
        {
            throw new ConflictException($"Fattura elettronica non generabile: {string.Join("; ", motivi)}.");
        }

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
    /// La descrizione della riga è obbligatoria nel tracciato della fattura elettronica: vuota
    /// produce un file che lo SDI scarta giorni dopo, quando di quella fattura non si ricorda più
    /// niente. Finora la pretendeva solo il dialogo dell'interfaccia, e bastava creare o modificare
    /// la fattura da un altro punto perché il controllo sparisse.
    /// </summary>
    private static void EsigiDescrizione(string? descrizione)
    {
        if (string.IsNullOrWhiteSpace(descrizione))
        {
            throw new ConflictException("La descrizione della fattura è obbligatoria: senza, la fattura elettronica verrebbe scartata.");
        }
    }

    private static string CostruisciCustomerKey(Ospite capofila) =>
        $"{capofila.NumeroDocumento}-{capofila.Nome}-{capofila.Cognome}-{capofila.DataNascita:yyyyMMdd}";

    private async Task<DatiCliente> CostruisciClienteBareBonesAsync(Guid strutturaId, Ospite capofila, string customerKey, CancellationToken cancellationToken) => new()
    {
        StrutturaId = strutturaId,
        Nome = capofila.Nome,
        Cognome = capofila.Cognome,
        LuogoResidenza = capofila.LuogoResidenza,
        Cittadinanza = capofila.Cittadinanza,
        Iso2 = await RisolviIso2DaCittadinanzaAsync(capofila.Cittadinanza, cancellationToken),
        // Solo per suggerire in automatico il Codice Fiscale nel form "Dati cliente" — la scheda
        // ospiti li ha già raccolti, evita di richiederli una seconda volta all'operatore.
        DataNascita = capofila.DataNascita,
        Sesso = capofila.Sesso,
        LuogoNascita = capofila.LuogoNascita,
        CustomerKey = customerKey,
    };

    /// <summary>
    /// Acronimo ISO2 della nazione del destinatario fattura, dedotto dalla Cittadinanza della scheda
    /// ospiti: quel campo è la Descrizione di una riga della tabella Stati (es. "REGNO UNITO"), da cui
    /// si risale all'Acronimo della stessa riga (es. "GB") — serve nell'XML SDI (IdPaese) e decide
    /// anche se proporre il calcolo del Codice Fiscale, quindi non va lasciato da compilare a mano.
    /// Cittadinanza vuota, o scritta a mano e non presente in tabella, resta null: meglio un campo
    /// vuoto che un codice inventato su una fattura.
    /// </summary>
    private async Task<string?> RisolviIso2DaCittadinanzaAsync(string? cittadinanza, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cittadinanza))
        {
            return null;
        }

        var acronimo = await riferimenti.GetAcronimoStatoPerDescrizioneAsync(cittadinanza, cancellationToken);
        return string.IsNullOrWhiteSpace(acronimo) ? null : acronimo.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Trova o crea per davvero il Cliente fatturabile per l'ospite capofila — usata SOLO al momento
    /// di generare effettivamente la fattura (<see cref="CreaDaPrenotazioneAsync"/>), quando un Cliente
    /// con Id reale è indispensabile per il vincolo FK di DatiFattura. Mai per una semplice anteprima:
    /// vedi <see cref="RisolviClientePerPrenotazioneAsync"/>, che non scrive nulla. CustomerKey =
    /// NumeroDocumento-Nome-Cognome-DataNascita, stessa formula del legacy per deduplicare i clienti —
    /// mai risolto due volte in un Cliente diverso per lo stesso ospite.
    /// </summary>
    private async Task<(DatiCliente Cliente, bool AppenaCreato)> RisolviOCreaClienteAsync(Guid strutturaId, Ospite capofila, CancellationToken cancellationToken)
    {
        var customerKey = CostruisciCustomerKey(capofila);

        var esistente = await clienti.GetByCustomerKeyAsync(strutturaId, customerKey, cancellationToken);
        if (esistente is not null)
        {
            return (esistente, false);
        }

        var nuovo = await CostruisciClienteBareBonesAsync(strutturaId, capofila, customerKey, cancellationToken);
        await clienti.AddAsync(nuovo, cancellationToken);
        return (nuovo, true);
    }

    /// <summary>
    /// Anteprima, SENZA SCRIVERE NULLA, del Cliente fatturabile per la prenotazione scelta nel dialog
    /// "Nuova fattura"/"Genera fattura": se esiste già (stessa CustomerKey) lo restituisce così com'è;
    /// altrimenti propone un Cliente bare-bones (nome/cognome/residenza/cittadinanza/data e comune di
    /// nascita dalla scheda ospiti) NON persistito — Id vuoto — da completare nel form. Viene creato
    /// per davvero solo quando l'operatore preme "Crea cliente" lì (<see cref="DatiClienteService.CreaAsync"/>,
    /// che riceve la stessa CustomerKey) oppure, se l'operatore salta quel passaggio, quando preme
    /// "Crea fattura" (<see cref="CreaDaPrenotazioneAsync"/>) — mai qui: altrimenti resterebbe un
    /// Cliente bare-bones orfano nel database ogni volta che si apre "Genera fattura" e poi si annulla
    /// senza completare né fatturare nulla.
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

        var customerKey = CostruisciCustomerKey(capofila);
        var esistente = await clienti.GetByCustomerKeyAsync(strutturaId, customerKey, cancellationToken);
        if (esistente is not null)
        {
            return (esistente, false);
        }

        return (await CostruisciClienteBareBonesAsync(strutturaId, capofila, customerKey, cancellationToken), true);
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
