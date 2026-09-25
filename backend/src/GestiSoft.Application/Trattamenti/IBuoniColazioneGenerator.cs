namespace GestiSoft.Application.Trattamenti;

/// <param name="Mattine">Un buono per ospite per ciascuna di queste date.</param>
/// <param name="EsercizioConvenzionato">Chi serve la colazione, se non la struttura stessa.</param>
public record DatiBuoniColazione(
    string NomeStruttura,
    string? EsercizioConvenzionato,
    string? NumeroPrenotazione,
    string? Camera,
    int Ospiti,
    IReadOnlyList<DateTime> Mattine);

/// <summary>PDF dei buoni colazione di una prenotazione, generato al volo e mai conservato, come le fatture.</summary>
public interface IBuoniColazioneGenerator
{
    byte[] Genera(DatiBuoniColazione dati);
}
