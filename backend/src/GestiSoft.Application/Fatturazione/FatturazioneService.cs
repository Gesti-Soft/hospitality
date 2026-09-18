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
    string? Divisa,
    /// <summary>Imposta di soggiorno da riaddebitare in fattura come riga esclusa art. 15. Null la lascia fuori; il valore predefinito lo propone la prenotazione.</summary>
    decimal? ImpostaSoggiorno = null,
    /// <summary>Fattura (chi ha partita IVA) o ricevuta (locazione breve di un privato). Serie di numerazione distinte.</summary>
    TipoEmissioneDocumento TipoEmissione = TipoEmissioneDocumento.Fattura);

public record AggiornaFatturaRequest(
    Guid? DatiClienteId,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    string? Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    string? Divisa,
    decimal? ImpostaSoggiorno = null);

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

        var ricevuta = request.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var prezzoUnitario = request.PrezzoUnitario ?? prenotazione.ImportoTotale ?? 0;
        var prezzoTotale = request.Quantita * prezzoUnitario;
        var aliquotaPercentuale = !ricevuta && request.AliquotaIva is { } aliquota ? (decimal)aliquota / 100m : 0m;
        // L'imposta di soggiorno entra nel totale da pagare ma non nell'imponibile: l'ospite la versa
        // insieme al resto, il Comune la incassa tramite la struttura.
        var importoTotale = Math.Round(prezzoTotale * (1 + aliquotaPercentuale), 2, MidpointRounding.AwayFromZero) + (request.ImpostaSoggiorno ?? 0);

        var anno = DateTime.UtcNow.Year;
        for (var tentativo = 0; tentativo < MassimiTentativiProgressivo; tentativo++)
        {
            var progressivo = await fatture.GetMaxProgressivoAsync(strutturaId, anno, request.TipoEmissione, cancellationToken) + 1;

            var candidata = new DatiFattura
            {
                StrutturaId = strutturaId,
                PrenotazioneId = prenotazione.Id,
                DatiClienteId = cliente.Id,
                Progressivo = progressivo,
                NumeroDocumento = progressivo,
                TipoEmissione = request.TipoEmissione,
                // Una ricevuta di locazione breve è fuori dal campo IVA e non passa dallo SDI:
                // aliquota, natura, regime e tipo documento sono codici del tracciato elettronico e
                // su quel foglio non significano niente.
                TipoDocumento = ricevuta ? null : request.TipoDocumento,
                RegimeFiscale = ricevuta ? null : request.RegimeFiscale,
                DataDocumento = DateTime.UtcNow.Date,
                Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? "EUR" : request.Divisa,
                Descrizione = request.Descrizione,
                ImpostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno : null,
                ImportoBollo = CalcolaBollo(request.TipoEmissione, request.Natura, prezzoTotale, request.ImpostaSoggiorno ?? 0),
                Quantita = request.Quantita,
                PrezzoUnitario = prezzoUnitario,
                PrezzoTotale = prezzoTotale,
                ImportoTotale = importoTotale,
                AliquotaIva = ricevuta ? null : request.AliquotaIva,
                Natura = ricevuta ? null : request.Natura,
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
        entity.ImpostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno : null;
        entity.ImportoBollo = CalcolaBollo(entity.TipoEmissione, request.Natura, prezzoTotale, request.ImpostaSoggiorno ?? 0);
        entity.ImportoTotale = Math.Round(prezzoTotale * (1 + aliquotaPercentuale), 2, MidpointRounding.AwayFromZero) + (request.ImpostaSoggiorno ?? 0);
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

        if (fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta)
        {
            throw new ConflictException("Una ricevuta di locazione breve non ha una fattura elettronica: l'operazione è fuori dal campo IVA e non passa dallo SDI. Resta scaricabile il PDF.");
        }

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

    /// <summary>Sopra questa soglia le somme non soggette a IVA scontano il bollo (art. 13 Tariffa DPR 642/72).</summary>
    private const decimal SogliaBollo = 77.47m;

    private const decimal ImportoBolloVirtuale = 2.00m;

    /// <summary>
    /// IVA e bollo sono alternativi (art. 6 Tabella B DPR 642/72): dove c'è IVA il bollo non si paga
    /// mai, dove non c'è si paga sopra 77,47 €. La soglia si misura sulla sola parte <b>non</b>
    /// soggetta, non sul totale della fattura: in una fattura mista — soggiorno con IVA al 10% più
    /// imposta di soggiorno esclusa art. 15 — conta solo la seconda. In pratica un hotel in regime
    /// ordinario non lo paga quasi mai, un forfettario quasi sempre.
    /// <para>
    /// La riga del soggiorno si considera fuori dall'IVA quando porta una Natura: è la convenzione
    /// dello SDI, dove o c'è un'aliquota o c'è il motivo per cui non c'è.
    /// </para>
    /// </summary>
    public static decimal? CalcolaBollo(TipoEmissioneDocumento tipoEmissione, NaturaIva? natura, decimal prezzoTotale, decimal impostaSoggiorno)
    {
        // Una ricevuta di locazione breve è interamente fuori dal campo IVA: non ha una natura da
        // esporre, ma l'intero importo concorre alla soglia.
        var fuoriCampoIva = tipoEmissione == TipoEmissioneDocumento.Ricevuta || natura is not null;
        var nonSoggetto = (fuoriCampoIva ? prezzoTotale : 0m) + impostaSoggiorno;
        return nonSoggetto > SogliaBollo ? ImportoBolloVirtuale : null;
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
