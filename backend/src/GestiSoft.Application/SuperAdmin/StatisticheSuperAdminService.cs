using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;

namespace GestiSoft.Application.SuperAdmin;

/// <summary>
/// Pagina "Statistiche" del Super Admin (staff GestiSoft): panoramica business cross-Cliente/
/// Struttura (Clienti/Strutture attivi, trend nuovi Clienti, incassi per Cliente, classifica
/// strutture per fatturato/occupazione) e salute delle 4 integrazioni esterne per Struttura.
/// Controller separato da SuperAdminController (che gestisce le leve CRUD cross-cliente) per lo
/// stesso motivo per cui Finanze/Entrate/Spese sono controller distinti sullo stesso dominio.
/// </summary>
public class StatisticheSuperAdminService(IStatisticheSuperAdminRepository repository)
{
    public async Task<StatisticheSuperAdminResult> GetStatisticheAsync(ICurrentUser currentUser, int anno, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.GetStatisticheAsync(anno, cancellationToken);
    }

    /// <summary>Anni con almeno una prenotazione su una qualunque Struttura — su richiesta esplicita, il selettore anno non deve proporre anni sicuramente vuoti.</summary>
    public async Task<IReadOnlyList<int>> GetAnniDisponibiliAsync(ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        RichiediSuperAdmin(currentUser);
        return await repository.ListaAnniConDatiAsync(cancellationToken);
    }

    private static void RichiediSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin)
        {
            throw new ForbiddenException("Solo il Super Admin può accedere a questa dashboard.");
        }
    }
}
