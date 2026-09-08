using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Osservatorio;

/// <summary>
/// Sceglie l'implementazione di IOsservatorioClient da usare per un OsservatorioAppartamento, in
/// base al suo Provider — punto di estensione per aggiungere altri sistemi regionali (es. ROSS1000,
/// adottato da diverse Regioni) senza toccare OsservatorioInvioService/OsservatorioConfigService,
/// che restano scritti contro l'interfaccia e non contro un'implementazione specifica.
/// </summary>
public interface IOsservatorioClientResolver
{
    IOsservatorioClient Risolvi(ProviderOsservatorio provider);
}
