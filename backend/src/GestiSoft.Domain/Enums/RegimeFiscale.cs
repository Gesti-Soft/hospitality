namespace GestiSoft.Domain.Enums;

/// <summary>
/// Codici "Regime Fiscale" dello standard di fatturazione elettronica italiana (SDI).
/// Valori invariati rispetto al sistema legacy per compatibilità con l'XML della fattura elettronica.
/// </summary>
public enum RegimeFiscale
{
    RF01_Ordinario = 1,
    RF02_ContribuentiMinimi = 2,
    RF03_NuoveIniziativeProduttive = 3,
    RF04_AgricolturaEAttivitaConnesseEPesca = 4,
    RF05_VenditaSaliETabacchi = 5,
    RF06_CommercioFiammiferi = 6,
    RF07_Editoria = 7,
    RF08_GestioneServiziTelefoniaPubblica = 8,
    RF09_RevocaRegimeSaliETabacchi = 9,
    RF10_RevocaRegimeFiammiferi = 10,
    RF11_RevocaRegimeEditoria = 11,
    RF12_RevocaRegimeTelefoniaPubblica = 12,
    RF13_VenditeADomicilio = 13,
    RF14_ReseDocenti = 14,
    RF15_AgenzieViaggiETurismo = 15,
    RF16_IvaPerCassa = 16,
    RF17_IvaDiGruppo = 17,
    RF18_Agricoltura_IVA_Art34_Comma6 = 18,
    RF19_Forfettario = 19,
}
