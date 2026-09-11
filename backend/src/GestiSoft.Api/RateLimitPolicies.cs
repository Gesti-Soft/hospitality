namespace GestiSoft.Api;

/// <summary>Nomi delle policy di rate limiting, condivisi tra la registrazione (Program) e i controller che le applicano.</summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
}
