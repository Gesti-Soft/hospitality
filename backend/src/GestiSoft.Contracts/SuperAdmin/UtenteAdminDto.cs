namespace GestiSoft.Contracts.SuperAdmin;

public record UtenteAdminDto(
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
