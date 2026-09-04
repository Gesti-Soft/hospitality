namespace GestiSoft.Application.Auth;

/// <summary>
/// Astrazione dell'utente autenticato per la richiesta corrente. Implementata in GestiSoft.Api
/// leggendo i claim del JWT (l'Application layer non dipende da ASP.NET Core/HttpContext).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UtenteId { get; }

    string Email { get; }

    bool IsSuperAdmin { get; }

    /// <summary>Null per i Super Admin.</summary>
    Guid? ClienteId { get; }

    /// <summary>True solo per il titolare/account Cliente: libero accesso a tutte le Strutture del proprio Cliente.</summary>
    bool IsClienteAccount { get; }
}
