namespace GestiSoft.Contracts.Impostazioni;

public record ImpostazioniStrutturaDto(
    Guid StrutturaId,
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    TimeOnly? OraInvioGiornaliero,
    decimal? TassaSoggiornoPrezzo,
    int? TassaSoggiornoMaxGiorni,
    int? TassaSoggiornoEtaEsenzioneMinori,
    int? TassaSoggiornoEtaEsenzioneAnziani,
    decimal? TassaSoggiornoPercentualeResidenti,
    decimal? TassaSoggiornoPercentualeMinori,
    decimal? TassaSoggiornoPercentualeAnziani,
    string? ComuneAttivita);
