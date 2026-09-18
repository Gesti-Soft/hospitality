namespace GestiSoft.Domain.Enums;

/// <summary>
/// Che documento si sta emettendo. Non è una preferenza di stampa: dipende da chi ospita.
/// <list type="bullet">
/// <item><see cref="Fattura"/> — chi ha partita IVA (hotel, B&amp;B imprenditoriale, affittacamere,
/// anche in regime forfettario). Va trasmessa allo SDI e porta aliquota o natura.</item>
/// <item><see cref="Ricevuta"/> — locazione breve di un privato senza partita IVA: l'operazione è
/// fuori dal campo IVA, non esiste fattura elettronica da emettere e si consegna una ricevuta non
/// fiscale, con marca da bollo da 2 € sopra 77,47 €.</item>
/// </list>
/// Le due serie hanno numerazione propria: una ricevuta non deve consumare un numero di fattura,
/// e i buchi nella numerazione delle fatture vanno evitati.
/// </summary>
public enum TipoEmissioneDocumento
{
    Fattura = 1,
    Ricevuta = 2,
}
