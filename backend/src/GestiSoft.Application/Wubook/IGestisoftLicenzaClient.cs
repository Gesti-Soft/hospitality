namespace GestiSoft.Application.Wubook;

public record EventiNonLettiRisultato(string Status, IReadOnlyList<string> RCodes);

/// <summary>
/// Client verso il backend esterno "gestisoft" (già in produzione, NON riscritto — vedi
/// GestiSoftWeb/UserService), usato solo più per l'intercettazione delle prenotazioni Wubook
/// (POST {baseUrl}/wubook/events/*): Wubook notifica le nuove prenotazioni a gestisoft.it, non
/// direttamente a questo backend, quindi il polling minute-by-minute passa sempre da qui.
/// La licenza/credenziali Wubook (token+lcode+scadenza) sono invece gestite direttamente dal Super
/// Admin qui in GestiSoftGestionale (vedi WubookLicenzaService) — non più recuperate da
/// gestisoft.it/users/set-running come nel comportamento precedente.
/// </summary>
public interface IGestisoftLicenzaClient
{
    Task<EventiNonLettiRisultato> GetEventiNonLettiAsync(string token, CancellationToken cancellationToken);

    Task MarkReadAsync(string token, IReadOnlyList<string> rcodes, CancellationToken cancellationToken);
}
