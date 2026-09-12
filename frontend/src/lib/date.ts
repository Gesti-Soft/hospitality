/**
 * Il backend tratta le date come "locali alla struttura" ritaggate con Kind=Utc, non convertite
 * (vedi il commento in pages/operativo/DashboardPage.tsx). Per evitare che il fuso orario del browser
 * sposti il giorno quando si costruisce una richiesta, qui si serializza sempre una data-ora
 * locale senza suffisso "Z"/offset — mai `Date.toISOString()`, che shifterebbe la data.
 */
export function isoLocale(d: Date): string {
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}T00:00:00`
}

export function inizioGiornoLocale(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate())
}

export function aggiungiGiorni(d: Date, giorni: number): Date {
  const copia = inizioGiornoLocale(d)
  copia.setDate(copia.getDate() + giorni)
  return copia
}

export function differenzaGiorni(a: Date, b: Date): number {
  return Math.round((inizioGiornoLocale(a).getTime() - inizioGiornoLocale(b).getTime()) / 86_400_000)
}

/** Valore per un <input type="date">, dai componenti locali (mai toISOString, vedi sopra). */
export function formatoInputData(d: Date): string {
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

export function parsaInputData(valore: string): Date {
  const [yyyy, mm, dd] = valore.split('-').map(Number)
  return new Date(yyyy, mm - 1, dd)
}

/**
 * Le date arrivano dal backend come timestamp "locali alla struttura" ma serializzati con
 * suffisso UTC (vedi GestiSoftDbContext — sono ritaggate, non convertite). Per l'Italia
 * (sempre in anticipo su UTC) confrontare l'anno/mese/giorno letti in timezone locale del
 * browser resta corretto; non è una soluzione generale multi-fuso.
 */
export function isOggi(iso: string | null): boolean {
  if (!iso) return false
  const d = new Date(iso)
  const oggi = new Date()
  return d.getFullYear() === oggi.getFullYear() && d.getMonth() === oggi.getMonth() && d.getDate() === oggi.getDate()
}

/** Oggi o prima — usato per le partenze: un check-out dimenticato non deve sparire dalla lista il giorno dopo, resta finché non viene fatto. */
export function isOggiOPrima(iso: string | null): boolean {
  if (!iso) return false
  return inizioGiornoLocale(new Date(iso)) <= inizioGiornoLocale(new Date())
}

/**
 * Da istante ISO (UTC, come lo restituisce l'API) al formato richiesto da un input
 * `datetime-local`, che lavora sempre in ora locale: `YYYY-MM-DDTHH:mm`, senza fuso e senza secondi.
 * Serve per i campi in cui conta l'ora e non solo il giorno, come l'arrivo effettivo dell'ospite.
 */
export function perCampoDataOra(isoUtc: string): string {
  const d = new Date(isoUtc)
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  const hh = String(d.getHours()).padStart(2, '0')
  const min = String(d.getMinutes()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}T${hh}:${min}`
}
