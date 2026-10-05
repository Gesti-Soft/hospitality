namespace GestiSoft.Contracts.SuperAdmin;

public record DashboardSuperAdminDto(IReadOnlyList<ClienteAdminDto> Clienti, IReadOnlyList<UtenteAdminDto> Utenti);

public record ImpostaAttivoRequest(bool Attivo);

/// <summary>ClienteId null = la demo va a un Cliente demo nuovo, con NomeCliente ed Email del suo titolare.</summary>
public record CreaStrutturaDemoRequest(Guid? ClienteId, string? NomeCliente, string? Email);

public record ServiziStrutturaRequest(bool WubookAbilitato, bool AlloggiatiWebAbilitato, bool OsservatorioAbilitato, bool PayTouristAbilitato);

public record ResettaPasswordRequest(string NuovaPassword);

public record AggiornaUtenteRequest(string Email, string? Nome, string? Cognome, bool IsClienteAccount);
