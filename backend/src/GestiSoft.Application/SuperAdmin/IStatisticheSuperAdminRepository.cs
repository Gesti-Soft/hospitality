using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.SuperAdmin;

public record PanoramicaBusinessResult(int ClientiAttivi, int ClientiTotali, int StruttureAttive, int StruttureTotali);

public record TrendMensileResult(int Mese, int Conteggio);

public record IncassoPerClienteResult(Guid ClienteId, string RagioneSociale, decimal ImportoPagatoAnno, int NumeroStrutture);

public record ClassificaStrutturaResult(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, decimal Valore);

public record EsitoIntegrazioneResult(EsitoIntegrazione Stato, DateTime? UltimoInvioAtUtc, string? UltimoErrore);

public record SaluteIntegrazioneStrutturaResult(
    Guid StrutturaId,
    string NomeStruttura,
    string RagioneSocialeCliente,
    EsitoIntegrazioneResult AlloggiatiWeb,
    EsitoIntegrazioneResult Osservatorio,
    EsitoIntegrazioneResult PayTourist,
    EsitoIntegrazioneResult Wubook);

public record StatisticheSuperAdminResult(
    PanoramicaBusinessResult Panoramica,
    IReadOnlyList<TrendMensileResult> NuoviClientiPerMese,
    IReadOnlyList<IncassoPerClienteResult> IncassiPerCliente,
    IReadOnlyList<ClassificaStrutturaResult> ClassificaStruttureFatturato,
    IReadOnlyList<ClassificaStrutturaResult> ClassificaStruttureOccupazione,
    IReadOnlyList<SaluteIntegrazioneStrutturaResult> SaluteIntegrazioni);

/// <summary>
/// Aggregazioni cross-Cliente/Struttura per la pagina Statistiche Super Admin. Stesso principio di
/// ISuperAdminRepository.GetDashboardAsync: Clienti/Strutture/righe di integrazione (decine/centinaia
/// di righe) caricate in memoria e ricomposte con dizionari; Prenotazioni/Ospiti (potenzialmente
/// molte righe su tutti i tenant insieme) sempre aggregate in SQL per Struttura (mai caricate
/// intere), poi ricongiunte in memoria al piccolo dizionario Struttura→Cliente.
/// </summary>
public interface IStatisticheSuperAdminRepository
{
    Task<StatisticheSuperAdminResult> GetStatisticheAsync(int anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno una prenotazione su una qualunque Struttura — per il selettore anno, mai anni sicuramente vuoti.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(CancellationToken cancellationToken);
}
