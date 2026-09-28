using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Ospiti;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Camere;
using GestiSoft.Application.Riferimenti;
using GestiSoft.Application.Servizi;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// Una riga del documento. Tipo, PrenotazioneId e PrenotazioneServizioId dicono da dove viene (il
/// soggiorno, un servizio extra) e impediscono di fatturarla due volte; una riga scritta a mano è Altro.
/// Su una fattura ogni riga vuole l'aliquota o la natura; su una ricevuta nessuna delle due.
/// </summary>
public record RigaFatturaRichiesta(
    string? Descrizione,
    decimal Quantita,
    decimal PrezzoUnitario,
    AliquotaIva? AliquotaIva,
    NaturaIva? Natura,
    TipoRigaFattura Tipo = TipoRigaFattura.Altro,
    Guid? PrenotazioneId = null,
    Guid? PrenotazioneServizioId = null);

public record CreaFatturaDaPrenotazioneRequest(
    /// <summary>Le prenotazioni fatturate: la prima è di chi paga, e il documento è intestato a lui.</summary>
    IReadOnlyList<Guid> PrenotazioneIds,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    string? Divisa,
    IReadOnlyList<RigaFatturaRichiesta> Righe,
    /// <summary>Imposta di soggiorno da riaddebitare in fattura come riga esclusa art. 15. Null la lascia fuori; il valore predefinito lo propone la prenotazione.</summary>
    decimal? ImpostaSoggiorno = null,
    /// <summary>Fattura (chi ha partita IVA) o ricevuta (locazione breve di un privato). Serie di numerazione distinte.</summary>
    TipoEmissioneDocumento TipoEmissione = TipoEmissioneDocumento.Fattura,
    /// <summary>Come ha pagato l'ospite: si conserva e si stampa solo sulla ricevuta.</summary>
    ModalitaPagamento? ModalitaPagamento = null);

public record AggiornaFatturaRequest(
    Guid? DatiClienteId,
    TipoDocumentoFattura? TipoDocumento,
    RegimeFiscale? RegimeFiscale,
    string? Divisa,
    IReadOnlyList<RigaFatturaRichiesta> Righe,
    decimal? ImpostaSoggiorno = null,
    ModalitaPagamento? ModalitaPagamento = null);

/// <summary>Righe proposte per fatturare una o più prenotazioni, con quello che l'operatore deve sapere.</summary>
/// <inheritdoc cref="GestiSoft.Contracts.Fatturazione.DaFatturareDto"/>
public record DaFatturare(bool SoggiornoFatturato, int ServiziDaFatturare, decimal ImportoServiziDaFatturare);

public record PropostaFattura(IReadOnlyList<RigaFatturaRichiesta> Righe, decimal ImpostaSoggiorno, IReadOnlyList<string> Avvisi);

/// <summary>
/// Fatture — porta FatturazioneViewModel del legacy (Sezione C del report Fase 4). La fattura
/// nasce sempre da una Prenotazione: il destinatario è l'Ospite capofila della scheda alloggiati
/// collegata, deduplicato tramite CustomerKey (NumeroDocumento-Nome-Cognome-DataNascita, stessa
/// formula del legacy). Il calcolo automatico del Codice Fiscale dall'anagrafica ospite (che il
/// legacy faceva con un generatore CF + tabella Belfiore) NON è stato portato — l'operatore lo
/// completa a mano sul DatiCliente, TODO per una fase dedicata se serve.
/// A differenza del legacy (dove Progressivo non veniva mai valorizzato, vedi report Sez. E.1-2),
/// qui è un vero contatore per (StrutturaId, Anno), con retry su conflitto di concorrenza.
/// <para>
/// Il documento ha più righe, ognuna con la sua aliquota o natura, e può fatturare più prenotazioni
/// (due famiglie, paga una). Il soggiorno di una prenotazione e ognuno dei suoi servizi extra si
/// fatturano una volta sola: dopo una fattura al check-in, una seconda può contenere solo gli
/// extra addebitati dopo.
/// </para>
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
    PermessoStrutturaGuard permessoGuard,
    IServizioStrutturaRepository servizi,
    ITipologiaCameraRepository tipologie)
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

    /// <summary>
    /// Stessa regola della proposta: soggiorno e ogni servizio extra si fatturano una volta sola. Serve
    /// a mostrare "2 servizi da fatturare" accanto al documento già emesso, per farne un secondo.
    /// </summary>
    public async Task<DaFatturare> DaFatturareAsync(ICurrentUser currentUser, Guid strutturaId, Guid prenotazioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        var prenotazione = (await CaricaPrenotazioniAsync(strutturaId, [prenotazioneId], cancellationToken))[0];

        var giaFatturate = await fatture.ListRigheFatturateAsync(strutturaId, [prenotazione.Id], escludiFatturaId: null, cancellationToken);
        var daFatturare = (await servizi.ListByPrenotazioneAsync(strutturaId, prenotazione.Id, cancellationToken))
            .Where(riga => !giaFatturate.Any(r => r.PrenotazioneServizioId == riga.Id))
            .ToList();

        return new DaFatturare(
            giaFatturate.Any(r => r.PrenotazioneId == prenotazione.Id && r.Tipo == TipoRigaFattura.Soggiorno),
            daFatturare.Count,
            daFatturare.Sum(ServiziService.Importo));
    }

    public async Task<DatiFattura> CreaDaPrenotazioneAsync(ICurrentUser currentUser, Guid strutturaId, CreaFatturaDaPrenotazioneRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var elencoPrenotazioni = await CaricaPrenotazioniAsync(strutturaId, request.PrenotazioneIds, cancellationToken);
        var principale = elencoPrenotazioni[0];

        var capofila = await ospiti.GetByPrenotazioneAsync(principale.Id, cancellationToken)
            ?? throw new ConflictException("La prenotazione di chi paga non ha ancora una scheda ospiti: compilala prima di fatturare.");

        var ricevuta = request.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var righe = await CostruisciRigheAsync(strutturaId, ricevuta, request.Righe, elencoPrenotazioni, fatturaId: null, cancellationToken);
        VerificaRegimeSenzaIva(ricevuta, request.RegimeFiscale, righe);
        var impostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno.Value : 0m;

        var (cliente, _) = await RisolviOCreaClienteAsync(strutturaId, capofila, cancellationToken);

        var anno = DateTime.UtcNow.Year;
        for (var tentativo = 0; tentativo < MassimiTentativiProgressivo; tentativo++)
        {
            var progressivo = await fatture.GetMaxProgressivoAsync(strutturaId, anno, request.TipoEmissione, cancellationToken) + 1;

            var candidata = new DatiFattura
            {
                StrutturaId = strutturaId,
                PrenotazioneId = principale.Id,
                DatiClienteId = cliente.Id,
                Progressivo = progressivo,
                NumeroDocumento = progressivo,
                TipoEmissione = request.TipoEmissione,
                ModalitaPagamento = ricevuta ? request.ModalitaPagamento : null,
                // Una ricevuta di locazione breve è fuori dal campo IVA e non passa dallo SDI:
                // aliquota, natura, regime e tipo documento sono codici del tracciato elettronico e
                // su quel foglio non significano niente.
                TipoDocumento = ricevuta ? null : request.TipoDocumento,
                RegimeFiscale = ricevuta ? null : request.RegimeFiscale,
                DataDocumento = DateTime.UtcNow.Date,
                Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? "EUR" : request.Divisa,
                Righe = righe.Select(Copia).ToList(),
                Prenotazioni = elencoPrenotazioni.Select(p => new FatturaPrenotazione { StrutturaId = strutturaId, PrenotazioneId = p.Id }).ToList(),
                ImpostaSoggiorno = impostaSoggiorno > 0 ? impostaSoggiorno : null,
                Anno = anno,
            };
            ApplicaTotali(candidata, impostaSoggiorno);

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

        // Stessa regola della creazione: il tipo di documento non cambia in modifica, e su una ricevuta
        // aliquota, natura, regime e tipo documento non esistono. Le prenotazioni fatturate restano
        // quelle scelte alla creazione.
        var ricevuta = entity.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var collegate = await CaricaPrenotazioniAsync(strutturaId, entity.Prenotazioni.Select(p => p.PrenotazioneId).ToList(), cancellationToken, obbligatorie: false);
        var righe = await CostruisciRigheAsync(strutturaId, ricevuta, request.Righe, collegate, entity.Id, cancellationToken);
        VerificaRegimeSenzaIva(ricevuta, request.RegimeFiscale, righe);
        var impostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno.Value : 0m;

        entity.DatiClienteId = request.DatiClienteId;
        entity.ModalitaPagamento = ricevuta ? request.ModalitaPagamento : null;
        entity.TipoDocumento = ricevuta ? null : request.TipoDocumento;
        entity.RegimeFiscale = ricevuta ? null : request.RegimeFiscale;
        entity.ImpostaSoggiorno = impostaSoggiorno > 0 ? impostaSoggiorno : null;
        entity.Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? entity.Divisa : request.Divisa;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await fatture.SostituisciRigheAsync(entity, righe.Select(Copia).ToList(), cancellationToken);
        ApplicaTotali(entity, impostaSoggiorno);
        await fatture.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Righe da proporre per fatturare le prenotazioni scelte: il soggiorno (l'importo totale meno i
    /// servizi extra, che vanno su righe loro con la loro aliquota, e meno la cauzione, che è un
    /// deposito e non un corrispettivo) e ogni servizio extra. Solo quello che non è già in un'altra
    /// fattura. Gli importi sono quelli della prenotazione, con l'IVA in aggiunta come sempre fatto
    /// finora (scelta dell'utente); l'operatore può cambiarli prima di emettere.
    /// </summary>
    public async Task<PropostaFattura> ProponiAsync(
        ICurrentUser currentUser,
        Guid strutturaId,
        IReadOnlyList<Guid> prenotazioneIds,
        TipoEmissioneDocumento tipoEmissione,
        CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);
        var elencoPrenotazioni = await CaricaPrenotazioniAsync(strutturaId, prenotazioneIds, cancellationToken);
        var ricevuta = tipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var azienda = await aziende.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        var (aliquotaPredefinita, naturaPredefinita) = ricevuta ? (null, null) : (azienda?.AliquotaIvaDefault, azienda?.NaturaDefault);
        // Il form dei dati aziendali mostra la natura solo con lo 0%: con un'aliquota vera una natura
        // rimasta salvata non vale.
        if (aliquotaPredefinita is { } predefinita && predefinita != AliquotaIva.Iva0)
        {
            naturaPredefinita = null;
        }

        // Forfettari e minimi non applicano l'IVA: 0% N2.2 su ogni riga, anche sui servizi che hanno
        // un'aliquota loro (pensata per chi è in regime ordinario).
        var senzaIva = !ricevuta && RegimeSenzaIva(azienda?.RegimeFiscale);
        if (senzaIva)
        {
            (aliquotaPredefinita, naturaPredefinita) = (AliquotaIva.Iva0, NaturaIva.N2_2_NonSoggetteAltriCasi);
        }
        var giaFatturate = await fatture.ListRigheFatturateAsync(strutturaId, elencoPrenotazioni.Select(p => p.Id).ToList(), escludiFatturaId: null, cancellationToken);

        var righe = new List<RigaFatturaRichiesta>();
        var avvisi = new List<string>();
        decimal impostaSoggiorno = 0;

        foreach (var prenotazione in elencoPrenotazioni)
        {
            var numero = prenotazione.NumeroPrenotazione is { Length: > 0 } n ? $"#{n}" : "senza numero";
            var righeServizio = await servizi.ListByPrenotazioneAsync(strutturaId, prenotazione.Id, cancellationToken);
            var totaleServizi = righeServizio.Sum(ServiziService.Importo);

            if (giaFatturate.FirstOrDefault(r => r.PrenotazioneId == prenotazione.Id && r.Tipo == TipoRigaFattura.Soggiorno) is { } soggiornoFatturato)
            {
                avvisi.Add($"Prenotazione {numero}: il soggiorno è già nella {NomeDocumento(soggiornoFatturato)}.");
            }
            else
            {
                var cauzione = prenotazione.CauzioneAttiva ? await CauzioneAsync(prenotazione, cancellationToken) : 0m;
                var importo = (prenotazione.ImportoTotale ?? 0) - totaleServizi - cauzione;
                if (cauzione > 0)
                {
                    avvisi.Add($"Prenotazione {numero}: la cauzione di {cauzione:0.00} € non è in fattura, è un deposito. Se è stata trattenuta, aggiungi una riga.");
                }

                if (importo <= 0)
                {
                    avvisi.Add($"Prenotazione {numero}: l'importo del soggiorno risulta {importo:0.00} €. Controlla l'importo totale della prenotazione.");
                }

                righe.Add(new RigaFatturaRichiesta(
                    DescrizioneSoggiorno(prenotazione), 1, Math.Max(importo, 0), aliquotaPredefinita, naturaPredefinita,
                    TipoRigaFattura.Soggiorno, prenotazione.Id));

                if (prenotazione.TassaSoggiornoAttiva && prenotazione.TotalTax is > 0)
                {
                    impostaSoggiorno += prenotazione.TotalTax.Value;
                }
            }

            var serviziGiaFatturati = 0;
            foreach (var riga in righeServizio)
            {
                if (giaFatturate.Any(r => r.PrenotazioneServizioId == riga.Id))
                {
                    serviziGiaFatturati++;
                    continue;
                }

                var listino = await servizi.GetAsync(strutturaId, riga.ServizioId, cancellationToken);
                var (aliquota, natura) = ricevuta
                    ? (null, null)
                    : senzaIva || listino is { AliquotaIva: null, Natura: null } or null
                        ? (aliquotaPredefinita, naturaPredefinita)
                        : (listino.AliquotaIva, listino.Natura);
                var notti = riga.Al is { } al ? al.DayNumber - riga.Dal.DayNumber : 0;
                var quantita = riga.Quantita * (ServiziService.PerNotte(riga.Modalita) ? notti : 1);
                righe.Add(new RigaFatturaRichiesta(
                    DescrizioneServizio(riga, notti), quantita, riga.PrezzoUnitario, aliquota, natura,
                    TipoRigaFattura.Servizio, prenotazione.Id, riga.Id));
            }

            if (serviziGiaFatturati > 0)
            {
                avvisi.Add($"Prenotazione {numero}: {serviziGiaFatturati} servizi extra sono già in un'altra fattura.");
            }
        }

        if (ricevuta && righe.Any(r => r.Tipo == TipoRigaFattura.Servizio))
        {
            avvisi.Add("Nella ricevuta di locazione breve va il canone: i servizi extra (escursioni, SPA…) non fanno parte della locazione. Senti il commercialista prima di includerli.");
        }

        return new PropostaFattura(righe, impostaSoggiorno, avvisi);
    }

    /// <summary>
    /// Regimi che non applicano l'IVA in fattura: forfettario (art. 1, commi 54-89, L. 190/2014) e
    /// contribuenti minimi. Ogni riga va a 0% con natura N2.2.
    /// </summary>
    public static bool RegimeSenzaIva(RegimeFiscale? regime) =>
        regime is RegimeFiscale.RF19_Forfettario or RegimeFiscale.RF02_ContribuentiMinimi;

    /// <summary>Un forfettario che addebita l'IVA emette una fattura sbagliata: si ferma qui, non allo SdI.</summary>
    private static void VerificaRegimeSenzaIva(bool ricevuta, RegimeFiscale? regime, IEnumerable<RigaFattura> righe)
    {
        if (!ricevuta && RegimeSenzaIva(regime) && righe.Any(r => r.AliquotaIva is { } a && a != AliquotaIva.Iva0))
        {
            throw new ConflictException("In regime forfettario o dei minimi non si applica l'IVA: tutte le righe vanno a 0% con natura N2.2.");
        }
    }

    public async Task<byte[]> GeneraPdfAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var (fattura, cliente, azienda) = await CaricaPerDocumentoAsync(currentUser, strutturaId, fatturaId, cancellationToken);
        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken);
        var soggiorno = await DatiSoggiornoAsync(fattura, strutturaId, cancellationToken);
        return documentGenerator.GeneraPdf(fattura, cliente, azienda, struttura?.Nome, soggiorno);
    }

    /// <summary>
    /// Solo per la ricevuta di locazione breve: è la ricevuta d'affitto che deve dire quale soggiorno
    /// paga (periodo, notti, ospiti, alloggio). La fattura la descrive già la riga del documento.
    /// </summary>
    private async Task<DatiSoggiorno?> DatiSoggiornoAsync(DatiFattura fattura, Guid strutturaId, CancellationToken cancellationToken)
    {
        if (fattura.TipoEmissione != TipoEmissioneDocumento.Ricevuta || fattura.PrenotazioneId is not { } prenotazioneId)
        {
            return null;
        }

        var prenotazione = await prenotazioni.GetAsync(prenotazioneId, cancellationToken);
        if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
        {
            return null;
        }

        var alloggio = !string.IsNullOrWhiteSpace(prenotazione.Camera?.Nome)
            ? prenotazione.Camera!.Nome
            : prenotazione.Tipologia?.TipologiaCamera;

        return new DatiSoggiorno(prenotazione.CheckIn, prenotazione.CheckOut, prenotazione.NumeroOspiti, alloggio, prenotazione.NumeroPrenotazione);
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

    /// <summary>Totali e bollo dalle righe: <see cref="CalcoloFattura"/>, la stessa regola di PDF e XML.</summary>
    private static void ApplicaTotali(DatiFattura fattura, decimal impostaSoggiorno)
    {
        fattura.PrezzoTotale = CalcoloFattura.Imponibile(fattura.Righe);
        fattura.ImportoTotale = CalcoloFattura.Totale(fattura.Righe, impostaSoggiorno);
        fattura.ImportoBollo = CalcoloFattura.Bollo(fattura.TipoEmissione, fattura.Righe, impostaSoggiorno);
    }

    private const int LunghezzaMassimaDescrizione = 1000;

    /// <summary>
    /// Le righe del documento, controllate. La descrizione è obbligatoria nel tracciato della fattura
    /// elettronica: vuota produce un file che lo SDI scarta giorni dopo. Su una fattura ogni riga vuole
    /// l'aliquota o la natura (mai tutte e due); su una ricevuta nessuna. Il soggiorno di una
    /// prenotazione e ogni suo servizio extra non possono stare in due documenti.
    /// </summary>
    private async Task<List<RigaFattura>> CostruisciRigheAsync(
        Guid strutturaId,
        bool ricevuta,
        IReadOnlyList<RigaFatturaRichiesta> richieste,
        IReadOnlyList<Prenotazione> prenotazioniDocumento,
        Guid? fatturaId,
        CancellationToken cancellationToken)
    {
        if (richieste is not { Count: > 0 })
        {
            throw new ConflictException("Il documento deve avere almeno una riga.");
        }

        var idPrenotazioni = prenotazioniDocumento.Select(p => p.Id).ToHashSet();
        var giaFatturate = await fatture.ListRigheFatturateAsync(strutturaId, idPrenotazioni.ToList(), fatturaId, cancellationToken);
        var righe = new List<RigaFattura>();

        foreach (var (richiesta, indice) in richieste.Select((r, i) => (r, i)))
        {
            var numero = indice + 1;
            var descrizione = richiesta.Descrizione?.Trim() ?? "";
            if (descrizione == "")
            {
                throw new ConflictException($"Riga {numero}: la descrizione è obbligatoria, senza la fattura elettronica verrebbe scartata.");
            }

            if (descrizione.Length > LunghezzaMassimaDescrizione)
            {
                throw new ConflictException($"Riga {numero}: la descrizione può avere al massimo {LunghezzaMassimaDescrizione} caratteri.");
            }

            if (richiesta.Quantita <= 0)
            {
                throw new ConflictException($"Riga {numero}: la quantità deve essere maggiore di zero.");
            }

            if (!Enum.IsDefined(richiesta.Tipo)
                || richiesta.AliquotaIva is { } a && !Enum.IsDefined(a)
                || richiesta.Natura is { } n && !Enum.IsDefined(n))
            {
                throw new ConflictException($"Riga {numero}: dati non riconosciuti.");
            }

            var (aliquota, natura) = ricevuta ? (null, null) : CalcoloFattura.AliquotaENatura(richiesta.AliquotaIva, richiesta.Natura, $"Riga {numero}");

            if (richiesta.PrenotazioneId is { } prenotazioneId && !idPrenotazioni.Contains(prenotazioneId))
            {
                throw new ConflictException($"Riga {numero}: la prenotazione non è tra quelle del documento.");
            }

            if (richiesta.Tipo == TipoRigaFattura.Soggiorno)
            {
                if (richiesta.PrenotazioneId is not { } soggiornoDi)
                {
                    throw new ConflictException($"Riga {numero}: una riga di soggiorno deve dire di quale prenotazione è.");
                }

                var doppione = giaFatturate.FirstOrDefault(r => r.PrenotazioneId == soggiornoDi && r.Tipo == TipoRigaFattura.Soggiorno);
                if (doppione is not null || righe.Any(r => r.Tipo == TipoRigaFattura.Soggiorno && r.PrenotazioneId == soggiornoDi))
                {
                    throw new ConflictException($"Riga {numero}: il soggiorno di questa prenotazione è già fatturato{(doppione is null ? "" : $" nella {NomeDocumento(doppione)}")}.");
                }
            }

            if (richiesta.Tipo == TipoRigaFattura.Servizio)
            {
                if (richiesta.PrenotazioneServizioId is not { } servizioId || richiesta.PrenotazioneId is not { } servizioDi)
                {
                    throw new ConflictException($"Riga {numero}: una riga di servizio extra deve dire quale servizio fattura.");
                }

                var dellaPrenotazione = await servizi.ListByPrenotazioneAsync(strutturaId, servizioDi, cancellationToken);
                if (dellaPrenotazione.All(r => r.Id != servizioId))
                {
                    throw new ConflictException($"Riga {numero}: il servizio extra non è di questa prenotazione.");
                }

                var doppione = giaFatturate.FirstOrDefault(r => r.PrenotazioneServizioId == servizioId);
                if (doppione is not null || righe.Any(r => r.PrenotazioneServizioId == servizioId))
                {
                    throw new ConflictException($"Riga {numero}: questo servizio extra è già fatturato{(doppione is null ? "" : $" nella {NomeDocumento(doppione)}")}.");
                }
            }

            righe.Add(new RigaFattura
            {
                StrutturaId = strutturaId,
                Numero = numero,
                Descrizione = descrizione,
                Quantita = richiesta.Quantita,
                PrezzoUnitario = richiesta.PrezzoUnitario,
                PrezzoTotale = CalcoloFattura.TotaleRiga(richiesta.Quantita, richiesta.PrezzoUnitario),
                AliquotaIva = aliquota,
                Natura = natura,
                Tipo = richiesta.Tipo,
                PrenotazioneId = richiesta.PrenotazioneId,
                PrenotazioneServizioId = richiesta.Tipo == TipoRigaFattura.Servizio ? richiesta.PrenotazioneServizioId : null,
            });
        }

        if (CalcoloFattura.Imponibile(righe) < 0)
        {
            throw new ConflictException("Il totale delle righe non può essere negativo.");
        }

        return righe;
    }

    private static RigaFattura Copia(RigaFattura r) => new()
    {
        StrutturaId = r.StrutturaId,
        Numero = r.Numero,
        Descrizione = r.Descrizione,
        Quantita = r.Quantita,
        PrezzoUnitario = r.PrezzoUnitario,
        PrezzoTotale = r.PrezzoTotale,
        AliquotaIva = r.AliquotaIva,
        Natura = r.Natura,
        Tipo = r.Tipo,
        PrenotazioneId = r.PrenotazioneId,
        PrenotazioneServizioId = r.PrenotazioneServizioId,
    };

    /// <summary>
    /// Le prenotazioni del documento, nell'ordine dato (la prima è di chi paga), tutte della struttura.
    /// Una annullata si può fatturare: una caparra trattenuta è un corrispettivo.
    /// </summary>
    private async Task<List<Prenotazione>> CaricaPrenotazioniAsync(Guid strutturaId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken, bool obbligatorie = true)
    {
        if (obbligatorie && ids is not { Count: > 0 })
        {
            throw new ConflictException("Scegli almeno una prenotazione da fatturare.");
        }

        if (ids.Distinct().Count() != ids.Count)
        {
            throw new ConflictException("La stessa prenotazione compare due volte.");
        }

        var elenco = new List<Prenotazione>();
        foreach (var id in ids)
        {
            var prenotazione = await prenotazioni.GetAsync(id, cancellationToken);
            if (prenotazione is null || prenotazione.StrutturaId != strutturaId)
            {
                throw new NotFoundException("Prenotazione non trovata.");
            }

            elenco.Add(prenotazione);
        }

        return elenco;
    }

    private async Task<decimal> CauzioneAsync(Prenotazione prenotazione, CancellationToken cancellationToken)
    {
        var tipologia = prenotazione.Tipologia
            ?? (prenotazione.Camera?.TipologiaId is { } tipologiaId ? await tipologie.GetAsync(tipologiaId, cancellationToken) : null);
        return tipologia?.Cauzione ?? 0m;
    }

    private static string DescrizioneSoggiorno(Prenotazione p)
    {
        var parti = new List<string>();
        if (p is { CheckIn: { } arrivo, CheckOut: { } partenza })
        {
            var notti = (partenza.Date - arrivo.Date).Days;
            parti.Add($"Soggiorno dal {arrivo:dd/MM/yyyy} al {partenza:dd/MM/yyyy} ({notti} {(notti == 1 ? "notte" : "notti")})");
        }
        else
        {
            parti.Add("Soggiorno");
        }

        if (p.Trattamento is { } trattamento)
        {
            parti[0] += $" con {Trattamenti.TrattamentiService.Nome(trattamento).ToLowerInvariant()}";
        }

        var alloggio = !string.IsNullOrWhiteSpace(p.Camera?.Nome) ? p.Camera!.Nome : p.Tipologia?.TipologiaCamera;
        if (!string.IsNullOrWhiteSpace(alloggio))
        {
            parti.Add(alloggio.Trim());
        }

        if (!string.IsNullOrWhiteSpace(p.NumeroPrenotazione))
        {
            parti.Add($"prenotazione #{p.NumeroPrenotazione}");
        }

        return string.Join(" — ", parti);
    }

    private static string DescrizioneServizio(PrenotazioneServizio r, int notti)
    {
        var quando = r.Al is { } al ? $"dal {r.Dal:dd/MM} al {al:dd/MM}" : $"del {r.Dal:dd/MM/yyyy}";
        var dettaglio = ServiziService.PerNotte(r.Modalita)
            ? $" ({r.Quantita} × {notti} {(notti == 1 ? "notte" : "notti")})"
            : r.Quantita > 1 ? $" ({r.Quantita} {(r.Modalita == ModalitaPrezzoServizio.APersona ? "persone" : "unità")})" : "";
        return $"{r.Nome} {quando}{dettaglio}";
    }

    private static string NomeDocumento(RigaFatturata r) =>
        $"{(r.TipoEmissione == TipoEmissioneDocumento.Ricevuta ? "ricevuta" : "fattura")} n. {r.NumeroDocumento}/{r.Anno}";

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
