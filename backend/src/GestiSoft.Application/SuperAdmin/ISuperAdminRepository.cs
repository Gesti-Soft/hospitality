namespace GestiSoft.Application.SuperAdmin;

public record StrutturaAdminInfo(
    Guid Id,
    string Nome,
    bool Attivo,
    DateTime? DisattivataAtUtc,
    bool WubookAttivo,
    string? WubookUltimoErrore,
    DateTime? ScadenzaLicenza,
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    bool WubookAbilitato,
    bool AlloggiatiWebAbilitato,
    bool OsservatorioAbilitato,
    bool PayTouristAbilitato);

public record ClienteAdminInfo(
    Guid Id,
    string RagioneSociale,
    string? PartitaIva,
    bool Attivo,
    DateTime CreatedAtUtc,
    decimal? QuotaAnnua,
    string? Note,
    int NumeroUtenti,
    int NumeroUtentiAttivi,
    IReadOnlyList<StrutturaAdminInfo> Strutture);

public record UtenteAdminInfo(
    Guid Id,
    string Email,
    string? Nome,
    string? Cognome,
    bool IsSuperAdmin,
    bool Attivo,
    Guid? ClienteId,
    string? ClienteRagioneSociale,
    DateTime CreatedAtUtc,
    bool IsClienteAccount);

public record DashboardSuperAdminInfo(IReadOnlyList<ClienteAdminInfo> Clienti, IReadOnlyList<UtenteAdminInfo> Utenti);

public interface ISuperAdminRepository
{
    Task<DashboardSuperAdminInfo> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Elimina DEFINITIVAMENTE una Struttura e tutti i dati collegati (camere, prenotazioni, ospiti,
    /// fatture, integrazioni...). Irreversibile — il chiamante (SuperAdminService) verifica prima che
    /// la Struttura sia disattivata da almeno 90 giorni.
    /// </summary>
    Task EliminaStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);
}
