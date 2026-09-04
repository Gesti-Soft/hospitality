using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.SuperAdmin;

public record PanoramicaBusinessResult(int ClientiAttivi, int ClientiTotali, int StruttureAttive, int StruttureTotali);

public record TrendMensileResult(int Mese, int Conteggio);

/// <summary>Incassi di GestiSoft stessa (rinnovi licenza Wubook pagati), non l'incasso del Cliente sulle proprie prenotazioni.</summary>
public record IncassoRinnovoMensileResult(int Mese, decimal Importo);

/// <summary>Licenza Wubook scaduta (o mai configurata) su una Struttura con Wubook concesso — il Cliente va sollecitato.</summary>
public record LicenzaScadutaResult(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, DateTime? Scadenza);

/// <summary>Licenza Wubook non ancora scaduta ma entro la finestra di preavviso — da rinnovare a breve.</summary>
public record LicenzaInScadenzaResult(Guid StrutturaId, string NomeStruttura, string RagioneSocialeCliente, DateTime Scadenza, int GiorniRimanenti);

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
    IReadOnlyList<IncassoRinnovoMensileResult> IncassiRinnoviPerMese,
    IReadOnlyList<LicenzaScadutaResult> LicenzeScadute,
    IReadOnlyList<LicenzaInScadenzaResult> LicenzeInScadenza,
    IReadOnlyList<SaluteIntegrazioneStrutturaResult> SaluteIntegrazioni);

/// <summary>
/// Aggregazioni cross-Cliente/Struttura per la pagina Statistiche Super Admin — solo il business di
/// GestiSoft stessa (Clienti/Strutture attivi, incassi da rinnovi licenza, chi deve pagare/sta per
/// scadere, salute integrazioni per supporto): niente sul fatturato/occupazione dei singoli Clienti,
/// che è affar loro, non di GestiSoft. Stesso principio di ISuperAdminRepository.GetDashboardAsync:
/// Clienti/Strutture/righe di integrazione (decine/centinaia di righe) caricate in memoria e
/// ricomposte con dizionari; solo NuoviClientiPerMese/IncassiRinnoviPerMese aggregati in SQL.
/// </summary>
public interface IStatisticheSuperAdminRepository
{
    Task<StatisticheSuperAdminResult> GetStatisticheAsync(int anno, CancellationToken cancellationToken);

    /// <summary>Anni con almeno una prenotazione su una qualunque Struttura — per il selettore anno, mai anni sicuramente vuoti.</summary>
    Task<IReadOnlyList<int>> ListaAnniConDatiAsync(CancellationToken cancellationToken);
}
