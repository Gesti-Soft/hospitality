import { tokens } from '../theme'

/**
 * Stessa palette usata dal backend per l'assegnazione automatica del colore alla creazione di un
 * canale vendita (CanaliVenditaService.PaletteColori) — tenerle allineate: un canale creato senza
 * colore esplicito riceve lo stesso risultato sia che la richiesta parta da qui sia da un altro
 * client dell'API.
 */
export const PALETTE_CANALI = [tokens.blue600, tokens.orange600, tokens.ok600, tokens.ink600, tokens.blue400, tokens.orange400, tokens.wait600, tokens.error600]

/** Colore suggerito per un nuovo canale, in base a quanti ne esistono già per la struttura. */
export function coloreAutomatico(numeroCanaliEsistenti: number): string {
  return PALETTE_CANALI[numeroCanaliEsistenti % PALETTE_CANALI.length]
}
