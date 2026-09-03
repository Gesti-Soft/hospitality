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

/**
 * Parsa una data digitata a mano in formato italiano "gg/mm/aaaa" (tollera "-"/"." come separatore,
 * o le 8 cifre senza separatori) — per lasciar scrivere direttamente l'anno in CampoData invece di
 * dover cliccare mese per mese per arrivare a una data lontana (es. una data di nascita). Restituisce
 * null se il testo non è un formato riconosciuto o la data "trabocca" (es. 31/02 non è il 3 marzo).
 */
export function parsaDataItaliana(testo: string): Date | null {
  const pulito = testo.trim()
  const soloNumeri = pulito.replace(/\D/g, '')

  let giorno: number
  let mese: number
  let anno: number

  if (/^\d{8}$/.test(soloNumeri) && !pulito.includes('/') && !pulito.includes('-') && !pulito.includes('.')) {
    giorno = Number(soloNumeri.slice(0, 2))
    mese = Number(soloNumeri.slice(2, 4))
    anno = Number(soloNumeri.slice(4, 8))
  } else {
    const match = pulito.match(/^(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})$/)
    if (!match) return null
    giorno = Number(match[1])
    mese = Number(match[2])
    anno = Number(match[3])
  }

  if (mese < 1 || mese > 12 || giorno < 1 || giorno > 31) return null

  const data = new Date(anno, mese - 1, giorno)
  if (data.getFullYear() !== anno || data.getMonth() !== mese - 1 || data.getDate() !== giorno) return null
  return data
}
