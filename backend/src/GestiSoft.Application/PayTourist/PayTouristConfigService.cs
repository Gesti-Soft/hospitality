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
