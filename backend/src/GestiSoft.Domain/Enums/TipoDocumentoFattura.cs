namespace GestiSoft.Domain.Enums;

/// <summary>
/// Codici "TipoDocumento" dello standard di fatturazione elettronica italiana (SDI).
/// Valori invariati rispetto al sistema legacy per compatibilità con l'XML della fattura elettronica.
/// </summary>
public enum TipoDocumentoFattura
{
    TD01_Fattura = 1,
    TD02_AccontoAnticipoSuFattura = 2,
    TD03_AccontoAnticipoSuParcella = 3,
    TD04_NotaDiCredito = 4,
    TD05_NotaDiDebito = 5,
    TD06_Parcella = 6,
    TD16_IntegrazioneFatturaReverseChargeInterno = 16,
    TD17_IntegrazioneAutofatturaAcquistoServiziEstero = 17,
    TD18_IntegrazioneAcquistoBeniIntracomunitari = 18,
    TD19_IntegrazioneAutofatturaAcquistoBeniArt17C2 = 19,
    TD20_AutofatturaRegolarizzazioneIntegrazioneFatture = 20,
    TD21_AutofatturaSplafonamento = 21,
    TD22_EstrazioneBeniDaDepositoIva = 22,
    TD23_EstrazioneBeniDaDepositoIvaConVersamentoIva = 23,
    TD24_FatturaDifferitaArt21Comma4LetteraA = 24,
    TD25_FatturaDifferitaArt21Comma4LetteraB = 25,
    TD26_CessioneBeniAmmortizzabiliPassaggiInterniArt36 = 26,
    TD27_FatturaAutoconsumoOCessioniGratuiteSenzaRivalsa = 27,
    TD28_AcquistiSanMarinoConIvaFatturaCartacea = 28,
    TD29_ComunicazioneOmessaOIrregolareFatturazioneArt6C8 = 29,
}
