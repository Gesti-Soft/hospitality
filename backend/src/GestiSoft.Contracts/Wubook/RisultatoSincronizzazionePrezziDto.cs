namespace GestiSoft.Contracts.Wubook;

/// <summary>Esito dell'invio prezzi all'OTA: un avviso per ogni tratto di giorni rimasto senza prezzo, e quindi non inviato.</summary>
public record RisultatoSincronizzazionePrezziDto(IReadOnlyList<string> Avvisi);
