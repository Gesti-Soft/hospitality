using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Auth;

/// <summary>
/// Verifica che il Super Admin abbia concesso un dato servizio esterno (Wubook/Alloggiati
/// Web/Osservatorio/PayTourist) a questa specifica Struttura — non più al Cliente nel suo insieme,
/// dato che due Strutture dello stesso Cliente possono avere concessioni diverse — e
/// indipendentemente dai toggle self-service dell'operatore (ImpostazioniStruttura/
/// WubookIntegrazione.Attivo), che restano un livello di controllo distinto e più fine. Se il
/// servizio non è concesso, l'azione va rifiutata (409/403) anche se il toggle self-service
/// risultasse acceso: la revoca del Super Admin deve prevalere sempre, sia per le azioni manuali
/// sia per i job automatici (nessun ICurrentUser richiesto qui apposta, per poter essere chiamata
/// anche dai job Quartz).
/// </summary>
public class ConcessioneServiziGuard(IStrutturaRepository strutture)
{
    public Task EnsureWubookAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        EnsureAsync(strutturaId, s => s.WubookAbilitato, "OTA", cancellationToken);

    public Task EnsureAlloggiatiWebAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        EnsureAsync(strutturaId, s => s.AlloggiatiWebAbilitato, "Alloggiati Web", cancellationToken);

    public Task EnsureOsservatorioAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        EnsureAsync(strutturaId, s => s.OsservatorioAbilitato, "Osservatorio Turistico", cancellationToken);

    public Task EnsurePayTouristAsync(Guid strutturaId, CancellationToken cancellationToken) =>
        EnsureAsync(strutturaId, s => s.PayTouristAbilitato, "PayTourist", cancellationToken);

    private async Task EnsureAsync(Guid strutturaId, Func<Struttura, bool> concesso, string nomeServizio, CancellationToken cancellationToken)
    {
        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken)
            ?? throw new NotFoundException("Struttura non trovata.");

        if (!concesso(struttura))
        {
            throw new ForbiddenException($"Il servizio {nomeServizio} non è abilitato per questa struttura. Contatta l'assistenza.");
        }
    }
}
