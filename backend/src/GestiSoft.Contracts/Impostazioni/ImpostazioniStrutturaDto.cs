namespace GestiSoft.Contracts.Impostazioni;

public record ImpostazioniStrutturaDto(
    Guid StrutturaId,
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    TimeOnly? OraInvioGiornaliero,
    decimal? TassaSoggiornoPrezzo,
    int? TassaSoggiornoMaxGiorni,
    string? ComuneAttivita);
