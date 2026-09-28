namespace GestiSoft.Domain.Enums;

/// <summary>
/// Quando è stato venduto un servizio extra: insieme alla prenotazione, oppure addebitato sul conto
/// durante il soggiorno (dopo il check-in), come SPA, bar o un'escursione prenotata alla reception.
/// </summary>
public enum OrigineServizio
{
    ConLaPrenotazione = 1,
    DuranteIlSoggiorno = 2,
}
