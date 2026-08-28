namespace GestiSoft.Contracts.SuperAdmin;

public record DashboardSuperAdminDto(IReadOnlyList<ClienteAdminDto> Clienti, IReadOnlyList<UtenteAdminDto> Utenti);

public record ImpostaAttivoRequest(bool Attivo);

public record ServiziStrutturaRequest(bool WubookAbilitato, bool AlloggiatiWebAbilitato, bool OsservatorioAbilitato, bool PayTouristAbilitato);

public record ResettaPasswordRequest(string NuovaPassword);

public record AggiornaUtenteRequest(string Email, string? Nome, string? Cognome);
