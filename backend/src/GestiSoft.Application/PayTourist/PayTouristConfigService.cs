using System.Globalization;
using System.Text.RegularExpressions;
using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Impostazioni;
using GestiSoft.Application.Logging;
using GestiSoft.Application.Wubook;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.PayTourist;

public record AggiornaPayTouristConfigRequest(string? Token, bool PortaleOnlineAttivo);

public record SalvaPayTouristStrutturaRequest(string? Nome, int? IdStrutturaPaytourist, IReadOnlyList<Guid> TipologieIds);

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

    public async Task<PayTouristIntegrazione> AggiornaConfigAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaPayTouristConfigRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await integrazioni.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new PayTouristIntegrazione { StrutturaId = strutturaId };

        entity.Token = RimuoviPrefissoBearer(request.Token);
        entity.PortaleOnlineAttivo = request.PortaleOnlineAttivo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await integrazioni.UpsertAsync(entity, cancellationToken);
        return entity;
    }

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
            idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(strutturaId, cancellationToken);
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

        var idSoftware = await wubookLicenzaService.GetIdPaytouristAsync(strutturaId, cancellationToken);
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
