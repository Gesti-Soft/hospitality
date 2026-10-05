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
    bool PayTouristAbilitato,
    bool Demo,
    DateTime? DemoEliminazioneAtUtc);

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
    /// la Struttura sia disattivata da almeno 90 giorni. Restituisce i percorsi delle foto dei ticket
    /// ancora sul disco, da cancellare dopo: il database non può annullare la cancellazione di un file.
    /// </summary>
    Task<IReadOnlyList<string>> EliminaStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    /// <summary>Strutture demo create prima di <paramref name="createPrimaDelUtc"/>: da eliminare.</summary>
    Task<IReadOnlyList<Guid>> ListaDemoScaduteAsync(DateTime createPrimaDelUtc, CancellationToken cancellationToken);

    /// <summary>Salva in un'unica transazione la struttura dimostrativa (vedi StrutturaDemoGenerator): o tutta, o niente.</summary>
    /// <param name="titolare">Accesso del Cliente demo nuovo, salvato nella stessa transazione; null se la demo va a un Cliente esistente.</param>
    Task AggiungiStrutturaDemoAsync(DatiStrutturaDemo dati, Domain.Entities.Utente? titolare, CancellationToken cancellationToken);
}
