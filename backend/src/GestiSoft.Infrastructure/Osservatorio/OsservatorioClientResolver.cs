using GestiSoft.Application.Osservatorio;
using GestiSoft.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Infrastructure.Osservatorio;

/// <summary>
/// Risolve IOsservatorioClient per Provider tramite i keyed services di .NET (vedi DependencyInjection.cs:
/// ogni implementazione viene registrata con chiave ProviderOsservatorio). Aggiungere un nuovo sistema
/// regionale significa solo scrivere la nuova classe e registrarla con la sua chiave — nessuna modifica qui.
/// </summary>
public class OsservatorioClientResolver(IServiceProvider serviceProvider) : IOsservatorioClientResolver
{
    public IOsservatorioClient Risolvi(ProviderOsservatorio provider) =>
        serviceProvider.GetRequiredKeyedService<IOsservatorioClient>(provider);
}
