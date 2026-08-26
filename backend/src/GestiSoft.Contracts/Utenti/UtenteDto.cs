namespace GestiSoft.Contracts.Utenti;

public record UtenteDto(Guid Id, string Email, string? Nome, string? Cognome, bool IsSuperAdmin, Guid? ClienteId, bool Attivo);
