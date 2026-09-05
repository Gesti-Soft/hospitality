/**
 * Calcolo del Codice Fiscale italiano (algoritmo standard, D.M. 12 marzo 1974) — usato come
 * suggerimento automatico, compilabile a mano, nel form "Dati cliente" quando il cliente è una
 * persona fisica italiana. Non gestisce l'omocodia (richiederebbe un archivio dei codici già
 * assegnati, non disponibile qui): il codice prodotto è quello "base", da correggere a mano nel
 * raro caso di omonimia con la stessa data e comune di nascita.
 */

const MESI = 'ABCDEHLMPRST' // gennaio=A, febbraio=B, ... dicembre=T (getMonth() è 0-based)

const VALORI_DISPARI: Record<string, number> = {
  '0': 1, '1': 0, '2': 5, '3': 7, '4': 9, '5': 13, '6': 15, '7': 17, '8': 19, '9': 21,
  A: 1, B: 0, C: 5, D: 7, E: 9, F: 13, G: 15, H: 17, I: 19, J: 21, K: 2, L: 4, M: 18, N: 20,
  O: 11, P: 3, Q: 6, R: 8, S: 12, T: 14, U: 16, V: 10, W: 22, X: 25, Y: 24, Z: 23,
}

const VALORI_PARI: Record<string, number> = {
  '0': 0, '1': 1, '2': 2, '3': 3, '4': 4, '5': 5, '6': 6, '7': 7, '8': 8, '9': 9,
  A: 0, B: 1, C: 2, D: 3, E: 4, F: 5, G: 6, H: 7, I: 8, J: 9, K: 10, L: 11, M: 12, N: 13,
  O: 14, P: 15, Q: 16, R: 17, S: 18, T: 19, U: 20, V: 21, W: 22, X: 23, Y: 24, Z: 25,
}

const VOCALI = 'AEIOU'

function normalizza(testo: string): string {
  return testo
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '') // rimuove i segni diacritici isolati dopo NFD (es. "È" -> "E" + accento)
    .toUpperCase()
    .replace(/[^A-Z]/g, '') // solo lettere: spazi, apostrofi e altri segni via
}

function consonantiEVocali(testo: string): { consonanti: string; vocali: string } {
  let consonanti = ''
  let vocali = ''
  for (const c of testo) {
    if (VOCALI.includes(c)) vocali += c
    else consonanti += c
  }
  return { consonanti, vocali }
}

function codiceCognome(cognome: string): string {
  const { consonanti, vocali } = consonantiEVocali(normalizza(cognome))
  return (consonanti + vocali + 'XXX').slice(0, 3)
}

function codiceNome(nome: string): string {
  const { consonanti, vocali } = consonantiEVocali(normalizza(nome))
  // Regola specifica del nome: con 4 o più consonanti si prendono 1ª, 3ª e 4ª (non le prime 3).
  if (consonanti.length >= 4) {
    return consonanti[0] + consonanti[2] + consonanti[3]
  }
  return (consonanti + vocali + 'XXX').slice(0, 3)
}

function codiceGiornoSesso(giorno: number, sesso: 1 | 2): string {
  const valore = sesso === 2 ? giorno + 40 : giorno
  return String(valore).padStart(2, '0')
}

function carattereControllo(quindiciCaratteri: string): string {
  let somma = 0
  for (let i = 0; i < quindiciCaratteri.length; i++) {
    const c = quindiciCaratteri[i]
    // Posizioni 1-based: le dispari (1ª, 3ª, ...) usano la tabella "dispari", le pari l'altra.
    somma += i % 2 === 0 ? VALORI_DISPARI[c] : VALORI_PARI[c]
  }
  return String.fromCharCode(65 + (somma % 26))
}

export interface DatiCalcoloCf {
  cognome: string
  nome: string
  dataNascita: Date
  sesso: 1 | 2
  /** Codice catastale Belfiore del comune di nascita (4 caratteri, es. "H501"). */
  codiceBelfiore: string
}

/** Restituisce il Codice Fiscale (16 caratteri) calcolato, o null se mancano dati sufficienti. */
export function calcolaCodiceFiscale({ cognome, nome, dataNascita, sesso, codiceBelfiore }: DatiCalcoloCf): string | null {
  const belfiore = codiceBelfiore.trim().toUpperCase()
  if (cognome.trim() === '' || nome.trim() === '' || belfiore.length < 4) return null

  const cCognome = codiceCognome(cognome)
  const cNome = codiceNome(nome)
  const cAnno = String(dataNascita.getFullYear()).slice(-2).padStart(2, '0')
  const cMese = MESI[dataNascita.getMonth()]
  const cGiorno = codiceGiornoSesso(dataNascita.getDate(), sesso)
  const cComune = belfiore.slice(0, 4)

  const base = `${cCognome}${cNome}${cAnno}${cMese}${cGiorno}${cComune}`
  return base + carattereControllo(base)
}
