using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
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
    ModalitaPagamento? ModalitaPagamento = null,
    /// <summary>A chi è intestato il documento, se non all'ospite di chi paga: una ditta dell'anagrafica clienti.</summary>
    Guid? DatiClienteId = null);

/// <summary>
/// Nota di credito (TD04) su una fattura già trasmessa: le righe dicono cosa si storna, tutto o in
/// parte. Gli importi sono positivi, è il tipo documento a dire che si tolgono.
/// </summary>
public record CreaNotaDiCreditoRequest(
    IReadOnlyList<RigaFatturaRichiesta> Righe,
    decimal? ImpostaSoggiorno = null);

/// <summary>Il file XML pronto da scaricare, con il nome che lo SdI si aspetta.</summary>
public record FileXmlFattura(string NomeFile, byte[] Contenuto);

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
    ITipologiaCameraRepository tipologie,
    ITrasmissioneFatturaRepository trasmissioni,
    FileSdiService fileSdi,
    IImpostazioniStrutturaRepository impostazioni,
    Sdi.IServizioSdi servizioSdi,
    ConcessioneServiziGuard concessioneGuard,
    Sdi.IRicevutaSdiPdfGenerator ricevutaPdf)
{
    private const int MassimiTentativiProgressivo = 5;

    public async Task<IReadOnlyList<DatiFattura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await fatture.ListAsync(strutturaId, anno, cancellationToken);
    }

    /// <summary>
    /// Senza canale la struttura carica le fatture da sé sul portale: il gestionale sa solo se ne ha
    /// scaricato l'XML, ed è quello che le sposta dalle "da inviare" allo storico.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> FattureConXmlScaricatoAsync(ICurrentUser currentUser, Guid strutturaId, IReadOnlyCollection<Guid> fatturaIds, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await trasmissioni.FattureConXmlScaricatoAsync(strutturaId, fatturaIds, cancellationToken);
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

        // Una ditta dell'anagrafica, se scelta; altrimenti il Cliente dell'ospite di chi paga. Per la
        // ditta non serve la scheda ospiti: il documento non porta i dati dell'ospite.
        DatiCliente cliente;
        if (request.DatiClienteId is { } clienteId)
        {
            cliente = await clienti.GetAsync(clienteId, cancellationToken) is { } scelto && scelto.StrutturaId == strutturaId
                ? scelto
                : throw new NotFoundException("Cliente non trovato.");
        }
        else
        {
            var capofila = await ospiti.GetByPrenotazioneAsync(principale.Id, cancellationToken)
                ?? throw new ConflictException("La prenotazione di chi paga non ha ancora una scheda ospiti: compilala prima di fatturare, oppure intesta il documento a un cliente dell'anagrafica.");
            (cliente, _) = await RisolviOCreaClienteAsync(strutturaId, capofila, cancellationToken);
        }

        var ricevuta = request.TipoEmissione == TipoEmissioneDocumento.Ricevuta;
        var righe = await CostruisciRigheAsync(strutturaId, ricevuta, request.Righe, elencoPrenotazioni, fatturaId: null, cancellationToken);
        VerificaRegimeSenzaIva(ricevuta, request.RegimeFiscale, righe);
        var impostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno.Value : 0m;

        if (!ricevuta && request.TipoDocumento == TipoDocumentoFattura.TD04_NotaDiCredito)
        {
            throw new ConflictException("Una nota di credito si emette dalla fattura che storna, con \"Storna con nota di credito\": così resta collegata all'originale.");
        }

        return await NumeraEAggiungiAsync(strutturaId, request.TipoEmissione, progressivo => new DatiFattura
        {
            StrutturaId = strutturaId,
            PrenotazioneId = principale.Id,
            DatiClienteId = cliente.Id,
            TipoEmissione = request.TipoEmissione,
            ModalitaPagamento = request.ModalitaPagamento,
            // Una ricevuta di locazione breve è fuori dal campo IVA e non passa dallo SDI:
            // aliquota, natura, regime e tipo documento sono codici del tracciato elettronico e
            // su quel foglio non significano niente.
            TipoDocumento = ricevuta ? null : request.TipoDocumento,
            RegimeFiscale = ricevuta ? null : request.RegimeFiscale,
            StatoSdi = ricevuta ? null : StatoSdiFattura.DaInviare,
            Divisa = string.IsNullOrWhiteSpace(request.Divisa) ? "EUR" : request.Divisa,
            Righe = righe.Select(Copia).ToList(),
            Prenotazioni = elencoPrenotazioni.Select(p => new FatturaPrenotazione { StrutturaId = strutturaId, PrenotazioneId = p.Id }).ToList(),
            ImpostaSoggiorno = impostaSoggiorno > 0 ? impostaSoggiorno : null,
        }, impostaSoggiorno, cancellationToken);
    }

    /// <summary>
    /// Numero e data al documento, poi l'inserimento. La data è quella civile italiana: in UTC, fra
    /// mezzanotte e l'una (le due d'estate) il documento prendeva il giorno prima, e a Capodanno
    /// l'anno prima. Il vincolo unico su (struttura, anno, serie, numero) fa da arbitro fra due
    /// emissioni contemporanee: chi perde ricalcola il numero e riprova.
    /// </summary>
    private async Task<DatiFattura> NumeraEAggiungiAsync(
        Guid strutturaId, TipoEmissioneDocumento serie, Func<int, DatiFattura> crea, decimal impostaSoggiorno, CancellationToken cancellationToken)
    {
        var oggi = Pulizie.PulizieSoggiornoService.Oggi();
        for (var tentativo = 0; tentativo < MassimiTentativiProgressivo; tentativo++)
        {
            var progressivo = await fatture.GetMaxProgressivoAsync(strutturaId, oggi.Year, serie, cancellationToken) + 1;

            var candidata = crea(progressivo);
            candidata.Progressivo = progressivo;
            candidata.NumeroDocumento = progressivo;
            candidata.DataDocumento = oggi;
            candidata.Anno = oggi.Year;
            ApplicaTotali(candidata, impostaSoggiorno);

            if (await fatture.TryAddAsync(candidata, cancellationToken))
            {
                return candidata;
            }
        }

        throw new ConflictException("Impossibile generare il numero fattura, riprova.");
    }

    /// <summary>
    /// Una fattura entrata nello SdI non si tocca più: per lo SdI esiste così, e si corregge solo con
    /// una nota di credito. Lo stesso vale mentre l'esito di un invio è ignoto: il file potrebbe essere
    /// arrivato, e cambiare la fattura farebbe partire alla prossima spedizione un documento diverso.
    /// Una scartata invece si corregge: per lo SdI non è mai esistita.
    /// </summary>
    private async Task VerificaModificabileAsync(DatiFattura fattura, CancellationToken cancellationToken)
    {
        switch (fattura.StatoSdi)
        {
            case StatoSdiFattura.Inviata:
                throw new ConflictException("La fattura è stata inviata allo SdI e si attende la ricevuta: non si può modificare.");
            case StatoSdiFattura.Consegnata or StatoSdiFattura.NonConsegnata:
                throw new ConflictException("La fattura è già stata trasmessa allo SdI e non si può più modificare. Per correggerla emetti una nota di credito.");
        }

        if (await trasmissioni.GetUltimaAsync(fattura.Id, cancellationToken) is { Esito: EsitoTrasmissioneSdi.EsitoIgnoto })
        {
            throw new ConflictException("L'ultimo invio allo SdI non ha avuto risposta e il file potrebbe essere arrivato: controlla sul portale e riprova l'invio prima di modificare la fattura.");
        }
    }

    public async Task<DatiFattura> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, AggiornaFatturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
        await VerificaModificabileAsync(entity, cancellationToken);

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
        var notaDiCredito = entity.FatturaStornataId is not null;
        // Le fatture di prima del canale (StatoSdi null) restano come sono: qualcuna è una TD04 scritta
        // a mano senza collegamento, e deve restare modificabile.
        if (!ricevuta && entity.StatoSdi is not null && notaDiCredito != (request.TipoDocumento == TipoDocumentoFattura.TD04_NotaDiCredito))
        {
            throw new ConflictException(notaDiCredito
                ? "Una nota di credito resta una nota di credito: per stornare in modo diverso emettine un'altra."
                : "Una nota di credito si emette dalla fattura che storna, con \"Storna con nota di credito\": così resta collegata all'originale.");
        }

        var collegate = await CaricaPrenotazioniAsync(strutturaId, entity.Prenotazioni.Select(p => p.PrenotazioneId).ToList(), cancellationToken, obbligatorie: false);
        var righe = await CostruisciRigheAsync(strutturaId, ricevuta, request.Righe, collegate, entity.Id, cancellationToken);
        VerificaRegimeSenzaIva(ricevuta, request.RegimeFiscale, righe);
        var impostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno.Value : 0m;

        if (entity.FatturaStornataId is { } stornataId)
        {
            await VerificaImportoStornabileAsync(strutturaId, stornataId, CalcoloFattura.Totale(righe, impostaSoggiorno), entity.Id, cancellationToken);
        }

        entity.DatiClienteId = request.DatiClienteId;
        entity.ModalitaPagamento = request.ModalitaPagamento;
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
    /// Le copie dei documenti emessi da una struttura che sta per essere eliminata: il PDF di ognuno,
    /// e per le fatture l'XML entrato nello SdI o scaricato per il portale, se c'è. Nessun controllo
    /// dei permessi: la chiama solo l'eliminazione definitiva, che l'ha già fatto (Super Admin o Worker).
    /// </summary>
    public async Task<IReadOnlyList<DocumentoFiscaleArchiviato>> DocumentiEmessiPerArchivioAsync(
        Struttura struttura, string clienteRagioneSociale, CancellationToken cancellationToken)
    {
        var azienda = await aziende.GetByStrutturaIdAsync(struttura.Id, cancellationToken);
        var elenco = await fatture.ListAsync(struttura.Id, anno: null, cancellationToken);
        var archiviati = new List<DocumentoFiscaleArchiviato>();
        foreach (var fattura in elenco)
        {
            var soggiorno = await DatiSoggiornoAsync(fattura, struttura.Id, cancellationToken);
            var data = DateOnly.FromDateTime(fattura.DataDocumento);
            var xml = fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta ? null : await XmlDaArchiviareAsync(struttura.Id, fattura.Id, cancellationToken);
            archiviati.Add(new DocumentoFiscaleArchiviato
            {
                ClienteId = struttura.ClienteId,
                ClienteRagioneSociale = clienteRagioneSociale,
                StrutturaIdOriginale = struttura.Id,
                StrutturaNome = struttura.Nome,
                StrutturaPartitaIva = azienda?.PIva,
                Tipo = fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta ? TipoDocumentoArchiviato.RicevutaLocazioneBreve
                    : fattura.TipoDocumento == TipoDocumentoFattura.TD04_NotaDiCredito ? TipoDocumentoArchiviato.NotaDiCredito
                    : TipoDocumentoArchiviato.FatturaEmessa,
                Numero = $"{fattura.NumeroDocumento}/{fattura.Anno}",
                Data = data,
                Controparte = fattura.Cliente is null ? null
                    : !string.IsNullOrWhiteSpace(fattura.Cliente.Denominazione) ? fattura.Cliente.Denominazione : $"{fattura.Cliente.Nome} {fattura.Cliente.Cognome}".Trim(),
                Importo = fattura.ImportoTotale,
                Pdf = documentGenerator.GeneraPdf(fattura, fattura.Cliente, azienda, struttura.Nome, soggiorno),
                File = xml?.Xml,
                NomeFile = xml?.NomeFile,
                Scadenza = DocumentoFiscaleArchiviato.ScadenzaPer(data),
            });
        }

        return archiviati;
    }

    /// <summary>
    /// Il file che conta: quello entrato nello SdI dal canale, o l'ultimo scaricato per caricarlo sul
    /// portale. Mai uno di collaudo, uno scartato o uno rifiutato: per il fisco non esistono.
    /// </summary>
    private async Task<TrasmissioneFattura?> XmlDaArchiviareAsync(Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        var elenco = await trasmissioni.ListByFatturaAsync(strutturaId, fatturaId, cancellationToken);
        var scelta = elenco.FirstOrDefault(t => !t.Prova && t.ViaCanale && t.Esito is (EsitoTrasmissioneSdi.Inviato or EsitoTrasmissioneSdi.Consegnato or EsitoTrasmissioneSdi.NonConsegnato))
            ?? elenco.FirstOrDefault(t => !t.Prova && !t.ViaCanale);
        return scelta is null ? null : await trasmissioni.GetAsync(strutturaId, scelta.Id, cancellationToken);
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

    /// <summary>
    /// Il file XML della fattura, col nome che lo SdI si aspetta. Se la struttura spedisce dal canale
    /// è il file che il canale spedirà (o ha spedito); altrimenti porta la struttura come trasmittente,
    /// perché sarà lei a caricarlo sul portale. Le fatture di prima del canale si scaricano sempre così.
    /// </summary>
    public async Task<FileXmlFattura> GeneraXmlSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        var fattura = await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);

        var viaCanale = fattura.StatoSdi is not null && await CanaleSdiAttivoAsync(strutturaId, cancellationToken);
        var file = await fileSdi.PreparaAsync(fattura, viaCanale, cancellationToken);
        return new FileXmlFattura(file.NomeFile, file.Xml);
    }

    /// <summary>Lo storico dei file XML della fattura, dal più recente.</summary>
    public async Task<IReadOnlyList<TrasmissioneFattura>> TrasmissioniAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
        return await trasmissioni.ListByFatturaAsync(strutturaId, fatturaId, cancellationToken);
    }

    /// <summary>Un file già generato, coi suoi byte: quello partito, non uno rigenerato.</summary>
    public async Task<FileXmlFattura> FileTrasmissioneAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, Guid trasmissioneId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        var trasmissione = await trasmissioni.GetAsync(strutturaId, trasmissioneId, cancellationToken);
        return trasmissione is not null && trasmissione.DatiFatturaId == fatturaId
            ? new FileXmlFattura(trasmissione.NomeFile, trasmissione.Xml)
            : throw new NotFoundException("File non trovato.");
    }

    /// <summary>Le ricevute dello SdI sui file della fattura, senza i byte: per mostrarle accanto a ogni file.</summary>
    public async Task<IReadOnlyList<RicevutaSdi>> RicevuteSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
        return await trasmissioni.RicevuteByFatturaAsync(strutturaId, fatturaId, cancellationToken);
    }

    /// <summary>
    /// Una ricevuta dello SdI esattamente com'è arrivata, firmata: per la consegna è la prova della data
    /// in cui la fattura è arrivata al cliente. Solo se è davvero di un file di questa fattura.
    /// </summary>
    public async Task<FileXmlFattura> FileRicevutaSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, Guid trasmissioneId, Guid ricevutaId, CancellationToken cancellationToken)
    {
        var ricevuta = await RicevutaSdiDellaFatturaAsync(currentUser, strutturaId, fatturaId, trasmissioneId, ricevutaId, cancellationToken);
        return new FileXmlFattura(NomeFileRicevuta(ricevuta), ricevuta.Xml);
    }

    /// <summary>La stessa ricevuta in PDF leggibile, per chi non sa aprire un XML firmato. Fa fede l'XML.</summary>
    public async Task<FileXmlFattura> PdfRicevutaSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, Guid trasmissioneId, Guid ricevutaId, CancellationToken cancellationToken)
    {
        var ricevuta = await RicevutaSdiDellaFatturaAsync(currentUser, strutturaId, fatturaId, trasmissioneId, ricevutaId, cancellationToken);
        var nomeXml = NomeFileRicevuta(ricevuta);
        return new FileXmlFattura(Path.ChangeExtension(nomeXml, ".pdf"), ricevutaPdf.GeneraPdf(ricevuta.Xml, nomeXml, ricevuta.Prova));
    }

    private async Task<RicevutaSdi> RicevutaSdiDellaFatturaAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, Guid trasmissioneId, Guid ricevutaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        var trasmissione = await trasmissioni.GetAsync(strutturaId, trasmissioneId, cancellationToken);
        var ricevuta = trasmissione is not null && trasmissione.DatiFatturaId == fatturaId
            ? await trasmissioni.GetRicevutaAsync(strutturaId, ricevutaId, cancellationToken)
            : null;
        return ricevuta is not null && ricevuta.TrasmissioneFatturaId == trasmissioneId
            ? ricevuta
            : throw new NotFoundException("Ricevuta non trovata.");
    }

    private static string NomeFileRicevuta(RicevutaSdi ricevuta) =>
        ricevuta.NomeFile ?? $"ricevuta-{ricevuta.IdentificativoSdi ?? ricevuta.Id.ToString()}.xml";

    /// <summary>
    /// Spedisce la fattura allo SdI dal canale, su richiesta dell'operatore. Qui si controlla chi la
    /// chiede; il servizio SdI ricontrolla concessione, interruttore e stato, spedisce e aggiorna la fattura.
    /// </summary>
    public async Task<EsitoInvioSdi> InviaSdiAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);
        await concessioneGuard.EnsureSdiAsync(strutturaId, cancellationToken);
        await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);
        return await servizioSdi.InviaFatturaAsync(strutturaId, fatturaId, currentUser.Email, cancellationToken);
    }

    /// <summary>
    /// Per chi consulta le fatture: se si spediscono dal canale. Le impostazioni della struttura le
    /// legge solo chi ha "gestione utenti", chi fattura no.
    /// </summary>
    public async Task<bool> CanaleSdiAttivoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await CanaleSdiAttivoAsync(strutturaId, cancellationToken);
    }

    /// <summary>Le fatture partono dal canale quando il Super Admin l'ha concesso e la struttura l'ha acceso.</summary>
    public async Task<bool> CanaleSdiAttivoAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        await strutture.GetByIdAsync(strutturaId, cancellationToken) is { SdiAbilitato: true }
        && await impostazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken) is { SdiAttivo: true };

    /// <summary>
    /// Nota di credito (TD04) che storna in tutto o in parte una fattura. Stesso cliente e stessa
    /// numerazione delle fatture, collegata all'originale. Le righe sono libere (non legate a soggiorno
    /// o servizi): uno storno totale rende di nuovo fatturabile la prenotazione, uno parziale no.
    /// <para>
    /// Si storna una fattura che lo SdI ha già: consegnata, non consegnata (è valida comunque), o
    /// emessa prima del canale. Una fattura ancora da inviare o scartata si corregge e basta, se la
    /// spedisce il canale; se la struttura la carica da sé, il gestionale non sa se è nello SdI e
    /// lascia decidere all'operatore.
    /// </para>
    /// </summary>
    public async Task<DatiFattura> CreaNotaDiCreditoAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaId, CreaNotaDiCreditoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);
        var originale = await GetOwnedAsync(strutturaId, fatturaId, cancellationToken);

        if (originale.TipoEmissione == TipoEmissioneDocumento.Ricevuta)
        {
            throw new ConflictException("Una ricevuta di locazione breve non si storna con una nota di credito.");
        }

        if (originale.TipoDocumento == TipoDocumentoFattura.TD04_NotaDiCredito || originale.FatturaStornataId is not null)
        {
            throw new ConflictException("Una nota di credito non si storna con un'altra nota di credito.");
        }

        switch (originale.StatoSdi)
        {
            case StatoSdiFattura.Inviata:
                throw new ConflictException("La fattura è in attesa della ricevuta dello SdI: aspetta l'esito prima di stornarla.");
            case StatoSdiFattura.Scartata:
                throw new ConflictException("La fattura è stata scartata dallo SdI, quindi non esiste: correggila e inviala di nuovo, senza nota di credito.");
            case StatoSdiFattura.DaInviare when await CanaleSdiAttivoAsync(strutturaId, cancellationToken):
                throw new ConflictException("La fattura non è ancora stata inviata allo SdI: correggila direttamente, senza nota di credito.");
        }

        var righe = await CostruisciRigheAsync(
            strutturaId, ricevuta: false,
            request.Righe.Select(r => r with { Tipo = TipoRigaFattura.Altro, PrenotazioneId = null, PrenotazioneServizioId = null }).ToList(),
            prenotazioniDocumento: [], fatturaId: null, cancellationToken);
        VerificaRegimeSenzaIva(ricevuta: false, originale.RegimeFiscale, righe);
        var impostaSoggiorno = request.ImpostaSoggiorno > 0 ? request.ImpostaSoggiorno.Value : 0m;
        await VerificaImportoStornabileAsync(strutturaId, originale.Id, CalcoloFattura.Totale(righe, impostaSoggiorno), escludiNotaId: null, cancellationToken);

        return await NumeraEAggiungiAsync(strutturaId, TipoEmissioneDocumento.Fattura, _ => new DatiFattura
        {
            StrutturaId = strutturaId,
            DatiClienteId = originale.DatiClienteId,
            TipoEmissione = TipoEmissioneDocumento.Fattura,
            TipoDocumento = TipoDocumentoFattura.TD04_NotaDiCredito,
            RegimeFiscale = originale.RegimeFiscale,
            StatoSdi = StatoSdiFattura.DaInviare,
            FatturaStornataId = originale.Id,
            Divisa = originale.Divisa ?? "EUR",
            Righe = righe.Select(Copia).ToList(),
            ImpostaSoggiorno = impostaSoggiorno > 0 ? impostaSoggiorno : null,
        }, impostaSoggiorno, cancellationToken);
    }

    /// <summary>Le note di credito di una fattura, insieme, non possono stornare più della fattura.</summary>
    private async Task VerificaImportoStornabileAsync(Guid strutturaId, Guid stornataId, decimal importoNota, Guid? escludiNotaId, CancellationToken cancellationToken)
    {
        var originale = await GetOwnedAsync(strutturaId, stornataId, cancellationToken);
        var giaStornato = await fatture.TotaleStornatoAsync(stornataId, escludiNotaId, cancellationToken);
        var residuo = originale.ImportoTotale - giaStornato;
        if (importoNota > residuo)
        {
            throw new ConflictException(giaStornato > 0
                ? $"La nota di credito ({importoNota:0.00} €) supera quanto resta da stornare della fattura: {residuo:0.00} € (già stornati {giaStornato:0.00} €)."
                : $"La nota di credito ({importoNota:0.00} €) supera il totale della fattura ({originale.ImportoTotale:0.00} €).");
        }
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
