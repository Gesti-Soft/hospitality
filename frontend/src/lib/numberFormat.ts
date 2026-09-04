import type { AxisValueFormatterContext } from '@mui/x-charts/models'

/**
 * Formatta un numero in forma compatta per le etichette degli assi dei grafici (es. 2550 → "2.55K",
 * 1250000 → "1.25M") — le etichette per esteso con separatore delle migliaia diventano illeggibili
 * sugli assi stretti. Sotto le mille resta il numero intero così com'è.
 */
export function formattaNumeroCompatto(valore: number): string {
  const segno = valore < 0 ? '-' : ''
  const assoluto = Math.abs(valore)
  if (assoluto >= 1_000_000) {
    return `${segno}${arrotondaSenzaZeriInutili(assoluto / 1_000_000)}M`
  }
  if (assoluto >= 1_000) {
    return `${segno}${arrotondaSenzaZeriInutili(assoluto / 1_000)}K`
  }
  return `${segno}${Math.round(assoluto)}`
}

function arrotondaSenzaZeriInutili(valore: number): string {
  return (Math.round(valore * 100) / 100).toString()
}

/**
 * Costruisce un `valueFormatter` per un asse numerico: forma compatta ("2.55K") sulle etichette
 * dell'asse, valore per esteso (passato da `formattatorePieno`, es. valuta) nel tooltip al passaggio
 * del mouse — non si perde precisione, solo l'asse resta leggibile.
 */
export function formattatoreAsseCompatto(formattatorePieno: (valore: number) => string) {
  return (valore: number, contesto: AxisValueFormatterContext) =>
    contesto.location === 'tick' ? formattaNumeroCompatto(valore) : formattatorePieno(valore)
}
