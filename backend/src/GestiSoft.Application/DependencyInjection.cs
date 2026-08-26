using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Punto di ingresso per la registrazione dei servizi applicativi (use case, validatori, mapper).
    /// Verrà popolato mano a mano che i moduli di business (camere, prenotazioni, fatturazione, ecc.)
    /// vengono portati dal sistema desktop.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
