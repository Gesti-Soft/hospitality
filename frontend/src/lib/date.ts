/**
 * Il backend tratta le date come "locali alla struttura" ritaggate con Kind=Utc, non convertite
 * (vedi il commento in pages/DashboardPage.tsx). Per evitare che il fuso orario del browser
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
