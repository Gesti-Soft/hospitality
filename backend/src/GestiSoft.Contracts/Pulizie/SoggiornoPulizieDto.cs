using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Pulizie;

/// <summary>
/// Una camera occupata vista da chi la pulisce: niente nome dell'ospite né importi, solo quello che
/// serve per preparare la camera (quante persone, per quanto, cosa tocca fare oggi).
/// </summary>
public record SoggiornoPulizieDto(
    Guid PrenotazioneId,
    string? CameraNome,
    string? TipologiaNome,
    int? NumeroOspiti,
    DateTime? Arrivo,
    DateTime? Partenza,
    int? NotteCorrente,
    int? NottiTotali,
    ServizioSoggiornoDto Pulizia,
    ServizioSoggiornoDto Biancheria);

public record ServizioSoggiornoDto(StatoServizioSoggiorno Stato, int? IntervalloGiorni, DateTime? Previsto, DateTime? UltimoFatto);

public record ImpostazioniPulizieTipologiaRequest(int? IntervalloPuliziaGiorni, int? IntervalloBiancheriaGiorni);

public record RinunceServiziRequest(bool RinunciaPulizia, bool RinunciaBiancheria);

public enum ServizioSoggiorno
{
    Pulizia = 1,
    Biancheria = 2,
}
