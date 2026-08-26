namespace GestiSoft.Domain.Enums;

/// <summary>
/// Codici "Natura IVA" dello standard di fatturazione elettronica italiana (SDI).
/// I valori numerici NON sono arbitrari: replicano i codici usati dal sistema legacy
/// e devono restare invariati per compatibilità con l'XML della fattura elettronica.
/// </summary>
public enum NaturaIva
{
    N1_EscluseArt15 = 1,
    N2_1_NonSoggetteArtt7_7Septies = 21,
    N2_2_NonSoggetteAltriCasi = 22,
    N3_1_NonImponibiliEsportazioni = 31,
    N3_2_NonImponibiliCessioniIntracomunitarie = 32,
    N3_3_NonImponibiliCessioniSanMarino = 33,
    N3_4_NonImponibiliAssimilateEsportazione = 34,
    N3_5_NonImponibiliDichiarazioniIntento = 35,
    N3_6_NonImponibiliAltreNoPlafond = 36,
    N4_Esenti = 4,
    N5_RegimeMargineIvaNonEsposta = 5,
    N6_1_ReverseChargeRottamiRecupero = 61,
    N6_2_ReverseChargeOroArgentoPuro = 62,
    N6_3_ReverseChargeSubappaltoEdile = 63,
    N6_4_ReverseChargeCessioneFabbricati = 64,
    N6_5_ReverseChargeTelefoniCellulari = 65,
    N6_6_ReverseChargeProdottiElettronici = 66,
    N6_7_ReverseChargePrestazioniEdiliConnesse = 67,
    N6_8_ReverseChargeSettoreEnergetico = 68,
    N6_9_ReverseChargeAltriCasi = 69,
    N7_IvaAssoltaAltroStatoUE = 7,
}
