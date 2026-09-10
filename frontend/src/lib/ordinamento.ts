/**
 * Confronto "naturale": alfabetico (A→Z) per il testo, ma numerico per le cifre al suo interno —
 * altrimenti un elenco di camere numerate ("1".."151") ordinerebbe come stringhe pure ("100" prima
 * di "11", "119" prima di "12", ecc.), bug reale su un pool di camere numerate in sequenza.
 */
export function confrontaNaturale(a: string, b: string): number {
  return a.localeCompare(b, 'it', { numeric: true, sensitivity: 'base' })
}
