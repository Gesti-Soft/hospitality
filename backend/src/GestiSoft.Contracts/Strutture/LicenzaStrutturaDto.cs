namespace GestiSoft.Contracts.Strutture;

/// <summary>Licenza software GestiSoft di una Struttura (solo Super Admin) — NON la licenza Wubook, vedi WubookLicenzaDto per quella.</summary>
public record LicenzaStrutturaDto(Guid StrutturaId, DateTime? ScadenzaLicenza);
