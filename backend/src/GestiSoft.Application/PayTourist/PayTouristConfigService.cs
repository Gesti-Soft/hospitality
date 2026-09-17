using System.Globalization;
using System.Text.RegularExpressions;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Prenotazioni;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.PayTourist;

/// <summary>
/// <see cref="PortaliAttivi"/> sono i portali per cui l'imposta la incassa il portale: nullo
/// significa "non toccare la selezione già salvata", una lista vuota significa "nessuno, la incasso
/// sempre io".
/// </summary>
public record AggiornaPayTouristConfigRequest(string? Token, bool PortaleOnlineAttivo, IReadOnlyList<PayTouristPortaleDto>? PortaliAttivi = null);

public record SalvaPayTouristStrutturaRequest(string? Nome, int? IdStrutturaPaytourist, IReadOnlyList<Guid> TipologieIds);

/// <summary>
/// Esito della domanda "su questo ente l'incasso tramite portali online è attivo?" — vedi
/// <see cref="PayTouristConfigService.VerificaPortaliOnlineAsync"/>. <see cref="Messaggio"/> è il
/// motivo da mostrare quando <see cref="Abilitato"/> è falso, e quando arriva da PayTourist viene
/// riportato parola per parola: è più preciso di qualunque riformulazione nostra.
/// </summary>
/// <param name="CanaliSenzaPortale">
/// Canali usati dalle prenotazioni che non corrispondono a nessun portale riconosciuto. L'abbinamento
/// è per nome esatto, e finora un nome diverso da quello di PayTourist (es. "Airbnb" contro "Airbnb
/// Ireland") faceva semplicemente partire la prenotazione come riscossa dalla struttura, senza che
/// nessuno potesse accorgersene.
/// </param>
public record VerificaPortaliOnlineDto(bool Abilitato, string? Messaggio, IReadOnlyList<PayTouristPortaleDto> Portali, IReadOnlyList<string> CanaliSenzaPortale);

/// <summary>
/// Suggerimento (non un dato autorevole) per le soglie età e le percentuali di riduzione di
/// Impostazioni → Tassa di soggiorno, ricavato da GET api/v1/reductions — vedi
/// <see cref="PayTouristConfigService.SuggerisciEtaEsenzioneTassaAsync"/> per i limiti di questa
/// estrazione. <see cref="Riduzioni"/> è l'elenco grezzo restituito da PayTourist, sempre incluso
/// perché l'operatore deve poter verificare/correggere il suggerimento prima di salvarlo, non
/// fidarsene alla cieca.
/// </summary>
public record SuggerimentoEtaTassaDto(
    int? EtaMinori,
    int? EtaAnziani,
    decimal? PercentualeResidenti,
    decimal? PercentualeMinori,
    decimal? PercentualeAnziani,
    IReadOnlyList<PayTouristRiduzioneDto> Riduzioni);

/// <summary>
/// Configurazione PayTourist per Struttura: il token (segreto, condiviso) e l'elenco delle
/// "strutture" PayTourist configurate (una Struttura di questo gestionale può averne più di una,
/// ognuna instrada un sottoinsieme di tipologie camera — porta PayTouristUser del legacy, stesso
/// pattern CRUD già usato per gli Appartamenti Osservatorio Turistico in Fase 7). Nessun flag di
/// permesso dedicato "PayTourist" nel modello UtenteStruttura: riusa StatePoliceSettings/
/// StatePoliceWrite/StatePoliceRead, stesso riuso pragmatico già fatto per Wubook/Alloggiati
/// Web/Osservatorio.
/// </summary>
public class PayTouristConfigService(
    IPayTouristIntegrazioneRepository integrazioni,
    IPayTouristStrutturaRepository strutture,
    IPayTouristClient client,
    IImpostazioniStrutturaRepository impostazioniStruttura,
    IPrenotazioneRepository prenotazioni,
    WubookLicenzaService wubookLicenzaService,
    IStrutturaRepository strutturaRepository,
    ILogEventoService logEventi,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<PayTouristIntegrazione> GetConfigAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        return await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };
    }

    /// <summary>
    /// <see cref="VerificaOk"/> è nullo quando non è stato indicato nessun token nuovo: non è
    /// "verifica fallita", è "non c'era niente da verificare".
    /// </summary>
    public async Task<(PayTouristIntegrazione Integrazione, bool? VerificaOk, string? VerificaErrore)> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaPayTouristConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };

        // Campo vuoto = "non toccare", mai "azzera": il token non viene mai rimandato al client, il
        // form lo mostra sempre vuoto, e chi salva per cambiare l'opzione qui sotto cancellerebbe
        // una credenziale funzionante senza averlo chiesto (richiesta esplicita dell'utente).
        var tokenRichiesto = RimuoviPrefissoBearer(request.Token);
        bool? verificaOk = null;
        string? verificaErrore = null;

        if (!string.IsNullOrWhiteSpace(tokenRichiesto))
        {
            tokenRichiesto = PulisciEValidaToken(tokenRichiesto);

            var comuneAttivita = (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita;
            var (ok, raggiungibile, errore) = await client.VerificaTokenAsync(tokenRichiesto, comuneAttivita, cancellationToken);

            await LogTokenAsync(
                currentUser,
                strutturaId,
                ok,
                ok ? "Token PayTourist salvato e verificato." : $"Token PayTourist {(raggiungibile ? "rifiutato dal portale" : "non verificabile")}: {errore}",
                cancellationToken);

            // Il portale ha risposto e ha rifiutato il token: non si salva, o un errore di battitura
            // sostituirebbe una credenziale funzionante e gli invii fallirebbero da lì in avanti.
            // Portale non raggiungibile: si salva comunque, dicendolo — un guasto di rete non può
            // impedire di configurare un token buono (scelta esplicita dell'utente).
            if (!ok && raggiungibile)
            {
                throw new ConflictException($"Token PayTourist rifiutato dal portale: {errore}");
            }

            entity.Token = tokenRichiesto;
            verificaOk = ok;
            verificaErrore = ok ? null : errore;
        }

        entity.PortaleOnlineAttivo = request.PortaleOnlineAttivo;

        if (request.PortaliAttivi is { } portaliScelti)
        {
            SincronizzaPortaliAttivi(entity, portaliScelti);
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;

        await integrazioni.UpsertAsync(entity, cancellationToken);
        return (entity, verificaOk, verificaErrore);
    }

    /// <summary>
    /// Allinea i portali salvati a quelli scelti, aggiungendo e togliendo solo ciò che cambia —
    /// stessa forma di SincronizzaTipologie per gli appartamenti Osservatorio. Il nome viene
    /// riscritto anche sulle righe che restano: se PayTourist rinomina un portale, l'abbinamento
    /// col canale della prenotazione si fa su quel nome, e tenerne uno vecchio lo farebbe fallire
    /// in silenzio.
    /// </summary>
    private static void SincronizzaPortaliAttivi(PayTouristIntegrazione entity, IReadOnlyList<PayTouristPortaleDto> scelti)
    {
        var richiesti = scelti.ToDictionary(p => p.Id, p => p.Nome);

        foreach (var daRimuovere in entity.PortaliAttivi.Where(p => !richiesti.ContainsKey(p.IdPortale)).ToList())
        {
            entity.PortaliAttivi.Remove(daRimuovere);
        }

        foreach (var esistente in entity.PortaliAttivi)
        {
            esistente.Nome = richiesti[esistente.IdPortale];
        }

        var giaPresenti = entity.PortaliAttivi.Select(p => p.IdPortale).ToHashSet();
        foreach (var (id, nome) in richiesti.Where(r => !giaPresenti.Contains(r.Key)))
        {
            entity.PortaliAttivi.Add(new PayTouristPortaleAttivo { PayTouristIntegrazioneId = entity.Id, IdPortale = id, Nome = nome });
        }
    }

    /// <summary>
    /// Il Token viaggia in un header HTTP, che ammette solo ASCII: una lettera accentata o uno
    /// spazio invisibile arrivati con il copia-incolla fanno fallire ogni chiamata con
    /// "Request headers must contain only ASCII characters", tradotto dal client in "servizio non
    /// raggiungibile" — chi salva legge un problema di rete al posto di un token sbagliato (successo
    /// dal vivo, con un token che conteneva una "è"). Spazi e caratteri a larghezza zero si tolgono,
    /// perché sono dell'incolla e non del token; su qualunque altro carattere estraneo il
    /// salvataggio si ferma dicendo dov'è, senza mai riportare il token.
    /// </summary>
    private static string PulisciEValidaToken(string token)
    {
        var pulito = new string(token.Where(c => !char.IsWhiteSpace(c) && c is not ('​' or '‌' or '‍' or '﻿')).ToArray());

        for (var i = 0; i < pulito.Length; i++)
        {
            if (pulito[i] is < ' ' or > '~')
            {
                throw new ConflictException(
                    $"Il token contiene un carattere non ammesso alla posizione {i + 1} (sono ammessi solo caratteri ASCII): controlla di averlo copiato per intero, senza lettere accentate.");
            }
        }

        return pulito;
    }

    /// <summary>Traccia chi ha cambiato il Token e com'è andata la verifica — mai il valore del token, che resta un segreto anche nei log.</summary>
    private async Task LogTokenAsync(ICurrentUser currentUser, Guid strutturaId, bool ok, string messaggio, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            ok ? LivelloLog.Info : LivelloLog.Warning,
            messaggio,
            origine: "PayTourist",
            clienteId: await strutturaRepository.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "PayTourist",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Il pannello PayTourist mostra il token già con il prefisso "Bearer " davanti — se l'operatore
    /// lo incolla per intero, l'header finirebbe con "Bearer" ripetuto due volte (lo aggiungiamo
    /// sempre noi in PayTouristClient) e PayTourist rifiuta un token altrimenti valido rispondendo
    /// "Unauthenticated". Tolleriamo qui il prefisso — maiuscolo/minuscolo, con o senza spazi
    /// attorno — invece di far scoprire il problema solo al primo invio reale (bug reale trovato
    /// testando con un account PayTourist di prova).
    /// </summary>
    private static string? RimuoviPrefissoBearer(string? token)
    {
        var pulito = token?.Trim();
        return pulito is not null && pulito.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? pulito[7..].Trim()
            : pulito;
    }

    public async Task<IReadOnlyList<PayTouristStruttura>> ListaStruttureAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);
        return await strutture.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    /// <summary>Strutture abilitate su PayTourist per il Token già configurato — pesca dal loro elenco account invece di far digitare a mano lo structure_id (vedi PayTouristStrutturaDialog).</summary>
    public async Task<IReadOnlyList<PayTouristStrutturaRemotaDto>> ListaStruttureDisponibiliAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (string.IsNullOrWhiteSpace(integrazione?.Token))
        {
            throw new ConflictException("Token PayTourist non configurato.");
        }

        var comuneAttivita = (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita;

        var (ok, elenco, errore) = await client.GetStruttureAsync(integrazione.Token, comuneAttivita, cancellationToken);
        if (!ok)
        {
            throw new ConflictException(errore ?? "Impossibile recuperare le strutture da PayTourist.");
        }

        return elenco;
    }

    /// <summary>
    /// Risponde a "l'incasso tramite portali online è attivo su questo ente?" prima che l'operatore
    /// accenda il filtro portale online: finché non c'era, l'opzione si poteva spuntare anche su un
    /// ente che non la prevede, e l'unico segnale arrivava la sera, come invio fallito. La domanda
    /// si può porre solo a PayTourist — nessun dato locale dice se il Comune l'ha attivata — e la
    /// risposta non viene salvata: l'ente può cambiarla quando vuole. Sola lettura, nessun invio.
    /// </summary>
    public async Task<VerificaPortaliOnlineDto> VerificaPortaliOnlineAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (string.IsNullOrWhiteSpace(integrazione?.Token))
        {
            throw new ConflictException("Token PayTourist non configurato: salvalo prima di attivare questa opzione.");
        }

        // L'endpoint dei portali vuole uno structure_id, ma la risposta riguarda l'ente, non la
        // singola struttura: va bene la prima configurata, senza chiedere all'operatore quale.
        var strutturePayTourist = await strutture.ListByStrutturaAsync(strutturaId, cancellationToken);
        if (strutturePayTourist.FirstOrDefault(s => s.IdStrutturaPaytourist is not null)?.IdStrutturaPaytourist is not { } idStrutturaPaytourist)
        {
            throw new ConflictException("Nessuna struttura PayTourist configurata con il suo Id: serve per interrogare il portale.");
        }

        var idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);
        var comuneAttivita = (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita;

        var (ok, portali, errore) = await client.GetPortaliOnlineAsync(integrazione.Token, comuneAttivita, idStrutturaPaytourist, idSoftware, cancellationToken);
        if (!ok)
        {
            return new VerificaPortaliOnlineDto(false, errore ?? "Impossibile verificare i portali online su PayTourist.", [], []);
        }

        // Ente abilitato ma senza nessun portale: non c'è niente da spuntare, quindi per chi
        // configura equivale a "non abilitato".
        if (portali.Count == 0)
        {
            return new VerificaPortaliOnlineDto(false, "Nessun portale online risulta abilitato su questo ente: non c'è niente da scegliere, l'imposta la incassi tu su ogni prenotazione.", [], []);
        }

        // I canali che nessun portale riconosce: vanno mostrati, perché è lì che si nasconde
        // l'errore silenzioso (un nome diverso e la prenotazione parte come riscossa dalla
        // struttura, senza nessun avviso).
        var nomiPortali = portali.Select(p => p.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canali = await prenotazioni.ListaAgenzieAsync(strutturaId, cancellationToken);

        return new VerificaPortaliOnlineDto(true, null, portali, canali.Where(c => !nomiPortali.Contains(c)).ToList());
    }

    public async Task<(PayTouristStruttura Struttura, bool ConnessioneOk, string? ConnessioneErrore)> CreaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = new PayTouristStruttura { StrutturaId = strutturaId };
        Applica(entity, request);

        var (ok, errore) = await VerificaConnessioneStrutturaAsync(strutturaId, entity, cancellationToken);
        if (ok)
        {
            entity.UltimaVerificaOkAtUtc = DateTime.UtcNow;
        }

        await strutture.AddAsync(entity, cancellationToken);
        await LogVerificaAsync(currentUser, strutturaId, entity.Nome, ok, errore, cancellationToken);
        return (entity, ok, errore);
    }

    public async Task<(PayTouristStruttura Struttura, bool ConnessioneOk, string? ConnessioneErrore)> AggiornaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, Guid payTouristStrutturaId, SalvaPayTouristStrutturaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await strutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura PayTourist non trovata.");

        Applica(entity, request);

        var (ok, errore) = await VerificaConnessioneStrutturaAsync(strutturaId, entity, cancellationToken);
        if (ok)
        {
            entity.UltimaVerificaOkAtUtc = DateTime.UtcNow;
        }

        await strutture.UpdateAsync(entity, cancellationToken);
        await LogVerificaAsync(currentUser, strutturaId, entity.Nome, ok, errore, cancellationToken);
        return (entity, ok, errore);
    }

    private async Task LogVerificaAsync(ICurrentUser currentUser, Guid strutturaId, string? nomeStruttura, bool ok, string? errore, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            ok ? LivelloLog.Info : LivelloLog.Warning,
            ok
                ? $"Verifica connessione PayTourist ({nomeStruttura}) riuscita."
                : $"Verifica connessione PayTourist ({nomeStruttura}) non riuscita: {errore}",
            origine: "PayTourist",
            clienteId: await strutturaRepository.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "PayTourist",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Test di connessione reale eseguito subito dopo il salvataggio (creazione o modifica) di una
    /// struttura PayTourist, su richiesta esplicita dell'utente — invece di scoprire un
    /// structure_id/token sbagliato solo al primo invio giornaliero reale. Usa GetRiduzioniAsync
    /// (sola lettura, nessun dato inviato) perché a differenza di GetStruttureAsync verifica proprio
    /// lo structure_id appena impostato, non solo token+Comune.
    /// </summary>
    private async Task<(bool Ok, string? Errore)> VerificaConnessioneStrutturaAsync(Guid strutturaId, PayTouristStruttura entity, CancellationToken cancellationToken)
    {
        if (entity.IdStrutturaPaytourist is not { } idStruttura)
        {
            return (false, "Id struttura PayTourist non impostato: verifica saltata.");
        }

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (string.IsNullOrWhiteSpace(integrazione?.Token))
        {
            return (false, "Token PayTourist non configurato: verifica saltata.");
        }

        int idSoftware;
        try
        {
            idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);
        }
        catch (ConflictException ex)
        {
            return (false, ex.Message);
        }

        var comuneAttivita = (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita;

        var (ok, _, errore) = await client.GetRiduzioniAsync(integrazione.Token, comuneAttivita, idStruttura, idSoftware, cancellationToken);
        return (ok, ok ? null : errore);
    }

    /// <summary>
    /// Legge le riduzioni configurate su PayTourist per questa struttura (stessa chiamata di
    /// <see cref="VerificaConnessioneStrutturaAsync"/>) e ne prova a indovinare le soglie età per
    /// minori/anziani, su richiesta esplicita — invece di far leggere e ricopiare a mano l'elenco
    /// all'operatore. È solo un SUGGERIMENTO best-effort: PayTourist non espone età/soglie come
    /// campo strutturato, solo testo libero diverso per ogni Comune (nome+descrizione) — qui si
    /// cerca un numero nel nome della riduzione (funziona per fascia scritta in cifre, es.
    /// "Minore Anni 12") o, in mancanza, un numero seguito da "anno" nella descrizione (funziona
    /// per fascia descritta a parole, es. "oltre il compimento del 75° anno di età"). Il chiamante
    /// deve sempre mostrare anche <see cref="SuggerimentoEtaTassaDto.Riduzioni"/> per la verifica
    /// manuale, mai salvare il suggerimento senza conferma.
    /// </summary>
    public async Task<SuggerimentoEtaTassaDto> SuggerisciEtaEsenzioneTassaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var integrazione = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (string.IsNullOrWhiteSpace(integrazione?.Token))
        {
            throw new ConflictException("Token PayTourist non configurato.");
        }

        var idStruttura = (await strutture.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Select(s => s.IdStrutturaPaytourist)
            .FirstOrDefault(id => id is not null);
        if (idStruttura is null)
        {
            throw new ConflictException("Nessuna struttura PayTourist con Id impostato per questa struttura.");
        }

        var idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(cancellationToken);
        var comuneAttivita = (await impostazioniStruttura.GetByStrutturaIdAsync(strutturaId, cancellationToken))?.ComuneAttivita;

        var (ok, riduzioni, errore) = await client.GetRiduzioniAsync(integrazione.Token, comuneAttivita, idStruttura.Value, idSoftware, cancellationToken);
        if (!ok)
        {
            throw new ConflictException(errore ?? "Impossibile recuperare le riduzioni da PayTourist.");
        }

        var minori = TrovaRiduzione(riduzioni, "minor");
        var anziani = TrovaRiduzione(riduzioni, "anzian", "ultra");
        var residenti = TrovaRiduzione(riduzioni, "resid");

        return new SuggerimentoEtaTassaDto(
            EstraiEta(minori),
            EstraiEta(anziani),
            EstraiPercentuale(residenti),
            EstraiPercentuale(minori),
            EstraiPercentuale(anziani),
            riduzioni);
    }

    // Esclude un numero seguito da "%": il nome porta quasi sempre la percentuale in fondo (es.
    // "Minore Anni 12 - 100%") — senza l'esclusione, un nome che non scrive l'età in cifre (es.
    // "Anziani Ultrasettantacinquenni - 100%") verrebbe letto erroneamente come età 100. I due \b
    // sono necessari: senza il confine di parola dopo \d+, il quantificatore greedy fa backtracking
    // da "100%" a "10" (seguito da "0%", non da "%" — supererebbe il controllo) invece di scartare
    // l'intero numero (bug reale riscontrato: suggeriva 10 invece di leggere "75" dalla descrizione).
    private static readonly Regex NumeroRegex = new(@"\b\d+\b(?!\s*%)", RegexOptions.Compiled);
    private static readonly Regex NumeroAnnoRegex = new(@"(\d+)\s*°?\s*ann", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>internal per essere testabile direttamente (vedi PayTouristConfigServiceTests) senza dover mockare l'intero servizio.</summary>
    internal static PayTouristRiduzioneDto? TrovaRiduzione(IReadOnlyList<PayTouristRiduzioneDto> riduzioni, params string[] paroleChiave) =>
        riduzioni.FirstOrDefault(r => paroleChiave.Any(p => r.Nome.Contains(p, StringComparison.OrdinalIgnoreCase)));

    internal static int? EstraiEta(PayTouristRiduzioneDto? candidata)
    {
        if (candidata is null)
        {
            return null;
        }

        if (NumeroRegex.Match(candidata.Nome) is { Success: true } daNome)
        {
            return int.Parse(daNome.Value);
        }

        var daDescrizione = candidata.Descrizione is null ? null : NumeroAnnoRegex.Match(candidata.Descrizione);
        return daDescrizione is { Success: true } ? int.Parse(daDescrizione.Groups[1].Value) : null;
    }

    /// <summary>Il campo "percentage" di PayTourist è un vero dato strutturato (a differenza dell'età) — es. "100.00" — basta il parsing, nessuna euristica sul testo.</summary>
    internal static decimal? EstraiPercentuale(PayTouristRiduzioneDto? candidata) =>
        candidata?.Percentuale is { } testo && decimal.TryParse(testo, NumberStyles.Number, CultureInfo.InvariantCulture, out var valore) ? valore : null;

    public async Task EliminaStrutturaAsync(ICurrentUser currentUser, Guid strutturaId, Guid payTouristStrutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await strutture.GetAsync(strutturaId, payTouristStrutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura PayTourist non trovata.");

        await strutture.DeleteAsync(entity, cancellationToken);
    }

    private void Applica(PayTouristStruttura entity, SalvaPayTouristStrutturaRequest request)
    {
        entity.Nome = request.Nome;
        entity.IdStrutturaPaytourist = request.IdStrutturaPaytourist;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        SincronizzaTipologie(entity, request.TipologieIds);
    }

    private void SincronizzaTipologie(PayTouristStruttura entity, IReadOnlyList<Guid> tipologieIds)
    {
        var richieste = tipologieIds.ToHashSet();

        foreach (var daRimuovere in entity.Tipologie.Where(t => !richieste.Contains(t.TipologiaId)).ToList())
        {
            entity.Tipologie.Remove(daRimuovere);
            strutture.RimuoviTipologia(daRimuovere);
        }

        var esistenti = entity.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        foreach (var tipologiaId in richieste.Where(id => !esistenti.Contains(id)))
        {
            entity.Tipologie.Add(new PayTouristStrutturaTipologia { PayTouristStrutturaId = entity.Id, TipologiaId = tipologiaId });
        }
    }
}
