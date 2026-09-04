namespace GestiSoft.Application.Auth;

/// <summary>Nomi dei claim custom nel JWT, condivisi tra generazione (Infrastructure) e lettura (Api).</summary>
public static class AppClaimTypes
{
    public const string IsSuperAdmin = "is_super_admin";

    public const string ClienteId = "cliente_id";

    public const string IsClienteAccount = "is_cliente_account";
}
