using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Osservatorio;

public record SalvaOsservatorioAppartamentoRequest(
    string? Nome,
    string? EntityCode,
    string? Password,
    string? HotelCode,
    IReadOnlyList<Guid> TipologieIds);

/// <summary>
/// Configurazione degli Appartamenti (entità PMS) Osservatorio Turistico per Struttura — una
/// Struttura può averne più di uno (ognuno instrada un sottoinsieme di tipologie camera, porta
/// GestiCache.Apartments del legacy). Nessun flag di permesso dedicato "Osservatorio" nel modello
/// UtenteStruttura: riusa <c>StatePoliceSettings</c>, stesso riuso pragmatico già fatto per
/// Alloggiati Web in Fase 6 e per Wubook in Fase 5.
/// </summary>
public class OsservatorioConfigService(
    IOsservatorioAppartamentoRepository repository,
    IOsservatorioClient client,
    IStrutturaRepository strutture,
    ILogEventoService logEventi,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<OsservatorioAppartamento>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);
        return await repository.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<(OsservatorioAppartamento Appartamento, bool ConnessioneOk, string? ConnessioneErrore)> CreaAsync(ICurrentUser currentUser, Guid strutturaId, SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = new OsservatorioAppartamento { StrutturaId = strutturaId };
        Applica(entity, request);

        var (ok, errore) = await VerificaConnessioneAsync(entity, cancellationToken);
        if (ok)
        {
            entity.UltimaVerificaOkAtUtc = DateTime.UtcNow;
        }

        await repository.AddAsync(entity, cancellationToken);
        await LogVerificaAsync(currentUser, strutturaId, entity.Nome, ok, errore, cancellationToken);
        return (entity, ok, errore);
    }

    public async Task<(OsservatorioAppartamento Appartamento, bool ConnessioneOk, string? ConnessioneErrore)> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, SalvaOsservatorioAppartamentoRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await repository.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        Applica(entity, request);

        var (ok, errore) = await VerificaConnessioneAsync(entity, cancellationToken);
        if (ok)
        {
            entity.UltimaVerificaOkAtUtc = DateTime.UtcNow;
        }

        await repository.UpdateAsync(entity, cancellationToken);
        await LogVerificaAsync(currentUser, strutturaId, entity.Nome, ok, errore, cancellationToken);
        return (entity, ok, errore);
    }

    private async Task LogVerificaAsync(ICurrentUser currentUser, Guid strutturaId, string? nomeAppartamento, bool ok, string? errore, CancellationToken cancellationToken) =>
        await logEventi.RegistraAsync(
            ok ? LivelloLog.Info : LivelloLog.Warning,
            ok
                ? $"Verifica connessione Osservatorio Turistico ({nomeAppartamento}) riuscita."
                : $"Verifica connessione Osservatorio Turistico ({nomeAppartamento}) non riuscita: {errore}",
            origine: "Osservatorio",
            clienteId: await strutture.GetClienteIdAsync(strutturaId, cancellationToken),
            strutturaId: strutturaId,
            categoria: "Osservatorio",
            operatore: currentUser.Email,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Test di connessione reale eseguito subito dopo il salvataggio (creazione o modifica) di un
    /// appartamento Osservatorio, su richiesta esplicita dell'utente — invece di scoprire
    /// EntityCode/Password/HotelCode sbagliati solo al primo invio giornaliero reale. Login +
    /// GetCurrentStatusDate sono entrambe sola lettura (nessuna Stay inviata), sempre seguite da
    /// Logout anche in caso di errore — stesso pattern try/finally già usato in
    /// OsservatorioInvioService.ProcessaAppartamentoAsync.
    /// </summary>
    private async Task<(bool Ok, string? Errore)> VerificaConnessioneAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.EntityCode) || string.IsNullOrWhiteSpace(entity.Password) || string.IsNullOrWhiteSpace(entity.HotelCode))
        {
            return (false, "EntityCode, password e HotelCode sono obbligatori per la verifica.");
        }

        var login = await client.LoginAsync(entity.EntityCode, entity.Password, cancellationToken);
        if (!login.Ok || login.Token is null)
        {
            return (false, login.Errore ?? "Credenziali non valide.");
        }

        try
        {
            var data = await client.GetCurrentStatusDateAsync(login.Token, entity.HotelCode, cancellationToken);
            return data is null ? (false, "HotelCode non valido o servizio non raggiungibile.") : (true, null);
        }
        finally
        {
            await client.LogoutAsync(login.Token, cancellationToken);
        }
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.StatePoliceSettings, cancellationToken);

        var entity = await repository.GetAsync(strutturaId, appartamentoId, cancellationToken)
            ?? throw new NotFoundException("Appartamento Osservatorio Turistico non trovato.");

        await repository.DeleteAsync(entity, cancellationToken);
    }

    private void Applica(OsservatorioAppartamento entity, SalvaOsservatorioAppartamentoRequest request)
    {
        entity.Nome = request.Nome;
        entity.EntityCode = request.EntityCode;
        entity.Password = request.Password;
        entity.HotelCode = request.HotelCode;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        SincronizzaTipologie(entity, request.TipologieIds);
    }

    private void SincronizzaTipologie(OsservatorioAppartamento entity, IReadOnlyList<Guid> tipologieIds)
    {
        var richieste = tipologieIds.ToHashSet();

        foreach (var daRimuovere in entity.Tipologie.Where(t => !richieste.Contains(t.TipologiaId)).ToList())
        {
            entity.Tipologie.Remove(daRimuovere);
            repository.RimuoviTipologia(daRimuovere);
        }

        var esistenti = entity.Tipologie.Select(t => t.TipologiaId).ToHashSet();
        foreach (var tipologiaId in richieste.Where(id => !esistenti.Contains(id)))
        {
            var riga = new OsservatorioAppartamentoTipologia { OsservatorioAppartamentoId = entity.Id, TipologiaId = tipologiaId };
            repository.AggiungiTipologia(riga);
            entity.Tipologie.Add(riga);
        }
    }
}
