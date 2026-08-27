namespace GestiSoft.Application.Wubook;

public record LicenzaRisultato(string Status, string? Messaggio, string? TokenWb, string? IdWoBook, string? IdPaytourist, bool IsRunning);

public record EventiNonLettiRisultato(string Status, IReadOnlyList<string> RCodes);

/// <summary>
/// Client verso il backend esterno "gestisoft" (licenze/abbonamenti, già in produzione, NON
/// riscritto — vedi GestiSoftWeb/UserService). Porta 1:1 il contratto di
/// POST {baseUrl}/users/set-running e POST {baseUrl}/wubook/events/*: la licenza del cliente
/// (username+token) e le credenziali Wubook (tokenWb/idWoBook) arrivano SEMPRE da qui, mai da
/// configurazione locale — su richiesta esplicita dell'utente, fedele al comportamento del
/// sistema legacy (GestiCache, mai persistito, sempre rinnovato).
/// </summary>
public interface IGestisoftLicenzaClient
{
    Task<LicenzaRisultato> SetRunningAsync(string username, string token, bool? isRunning, CancellationToken cancellationToken);

    Task<EventiNonLettiRisultato> GetEventiNonLettiAsync(string token, CancellationToken cancellationToken);

    Task MarkReadAsync(string token, IReadOnlyList<string> rcodes, CancellationToken cancellationToken);
}
