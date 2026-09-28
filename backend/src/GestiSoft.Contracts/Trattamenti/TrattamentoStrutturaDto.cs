using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Trattamenti;

/// <summary>
/// Listino di un trattamento della struttura, a persona e a notte. Prezzo bambini facoltativo (null
/// = pagano come gli adulti), in euro o in percentuale del prezzo adulto, fino a EtaMassimaBambini compresa.
/// StampaTicket: sulle prenotazioni con questo trattamento si stampano i ticket per l'esercizio convenzionato.
/// </summary>
public record TrattamentoStrutturaDto(
    TipoTrattamento Tipo,
    bool Attivo,
    decimal PrezzoPerPersona,
    decimal? PrezzoBambini,
    TipoVariazionePrezzo TipoPrezzoBambini,
    int? EtaMassimaBambini,
    string? EsercizioConvenzionato,
    bool StampaTicket = false);
