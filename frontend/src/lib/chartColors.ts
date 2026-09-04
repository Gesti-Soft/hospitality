import { tokens } from '../theme'

/**
 * Palette categorica per i grafici di Statistiche (agenzia/nazionalità/tipologia camera) — 7 hue
 * fisse in ordine fisso (mai cicliche, mai riassegnate in base al rank) più un grigio neutro
 * riservato alla voce aggregata "Altro" del Top7+Altro lato backend, per distinguerla a colpo
 * d'occhio dalle categorie reali. Tinte più profonde/desaturate del default dataviz generico, per
 * intonarsi allo stile "boutique" navy/terracotta già usato nel resto dell'app (blue600/orange600),
 * mantenendo la stessa famiglia di tonalità (blu, arancio, verde acqua, oro, magenta, verde, viola)
 * che garantisce la distinguibilità tra voci adiacenti. Validata con lo script dataviz sulla
 * superficie chiara dell'app (#FFFFFF): tutti i controlli passano (CVD adiacente ΔE 8.0, pavimento
 * 6; percezione normale ΔE 19.0, pavimento 15; contrasto ≥3:1 su tutte e 7).
 */
export const PALETTE_CATEGORICA = ['#1A6E9E', '#C1652E', '#0E8C78', '#B98A2E', '#B14C74', '#3E7A2E', '#6B4E96'] as const

export const COLORE_ALTRO = tokens.textTertiary

/** Assegna la palette categorica in ordine fisso alle etichette, con "Altro" sempre in grigio neutro. */
export function coloriPerEtichette(etichette: readonly string[]): string[] {
  let indice = 0
  return etichette.map((etichetta) => {
    if (etichetta === 'Altro') {
      return COLORE_ALTRO
    }
    const colore = PALETTE_CATEGORICA[indice % PALETTE_CATEGORICA.length]
    indice += 1
    return colore
  })
}
