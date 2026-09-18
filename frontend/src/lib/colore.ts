/**
 * Conversioni fra le tre forme in cui un colore viene scritto o mostrato: esadecimale (come sta nel
 * database dei canali vendita), RGB (come lo digita chi ha il valore dal grafico o dal sito di un
 * portale) e HSV (come lo si sceglie a mano, che è la forma su cui si muovono il quadrato e la
 * barra delle tonalità del selettore).
 *
 * HSV e non HSL perché il quadrato "saturazione × luminosità" del selettore è esattamente un piano
 * HSV a tonalità fissa: con HSL lo stesso quadrato richiederebbe una correzione a ogni movimento.
 */

export interface Rgb {
  r: number
  g: number
  b: number
}

/** Tonalità 0-360, saturazione e luminosità 0-100. */
export interface Hsv {
  h: number
  s: number
  v: number
}

const limita = (valore: number, minimo: number, massimo: number) => Math.min(Math.max(valore, minimo), massimo)

/**
 * Esadecimale normalizzato a "#RRGGBB" maiuscolo, o null se la stringa non è un colore valido.
 * Accetta la forma a 3 cifre (#1AF) e il cancelletto omesso, perché sono i due modi in cui un
 * colore viene incollato più spesso.
 */
export function normalizzaHex(testo: string): string | null {
  const pulito = testo.trim().replace(/^#/, '')

  if (/^[0-9a-fA-F]{3}$/.test(pulito)) {
    return `#${pulito
      .split('')
      .map((c) => c + c)
      .join('')}`.toUpperCase()
  }

  return /^[0-9a-fA-F]{6}$/.test(pulito) ? `#${pulito}`.toUpperCase() : null
}

/** Componenti RGB di un esadecimale; nero se la stringa non è un colore valido. */
export function hexToRgb(hex: string): Rgb {
  const normalizzato = normalizzaHex(hex)
  if (normalizzato === null) {
    return { r: 0, g: 0, b: 0 }
  }

  return {
    r: parseInt(normalizzato.slice(1, 3), 16),
    g: parseInt(normalizzato.slice(3, 5), 16),
    b: parseInt(normalizzato.slice(5, 7), 16),
  }
}

export function rgbToHex({ r, g, b }: Rgb): string {
  const cifre = (valore: number) => limita(Math.round(valore), 0, 255).toString(16).padStart(2, '0')
  return `#${cifre(r)}${cifre(g)}${cifre(b)}`.toUpperCase()
}

export function rgbToHsv({ r, g, b }: Rgb): Hsv {
  const rosso = limita(r, 0, 255) / 255
  const verde = limita(g, 0, 255) / 255
  const blu = limita(b, 0, 255) / 255

  const massimo = Math.max(rosso, verde, blu)
  const minimo = Math.min(rosso, verde, blu)
  const delta = massimo - minimo

  let h = 0
  if (delta !== 0) {
    if (massimo === rosso) {
      h = ((verde - blu) / delta) % 6
    } else if (massimo === verde) {
      h = (blu - rosso) / delta + 2
    } else {
      h = (rosso - verde) / delta + 4
    }
    h = (h * 60 + 360) % 360
  }

  return { h, s: massimo === 0 ? 0 : (delta / massimo) * 100, v: massimo * 100 }
}

export function hsvToRgb({ h, s, v }: Hsv): Rgb {
  const tonalita = ((h % 360) + 360) % 360
  const saturazione = limita(s, 0, 100) / 100
  const luminosita = limita(v, 0, 100) / 100

  const c = luminosita * saturazione
  const x = c * (1 - Math.abs(((tonalita / 60) % 2) - 1))
  const m = luminosita - c

  const [r, g, b] =
    tonalita < 60
      ? [c, x, 0]
      : tonalita < 120
        ? [x, c, 0]
        : tonalita < 180
          ? [0, c, x]
          : tonalita < 240
            ? [0, x, c]
            : tonalita < 300
              ? [x, 0, c]
              : [c, 0, x]

  return { r: Math.round((r + m) * 255), g: Math.round((g + m) * 255), b: Math.round((b + m) * 255) }
}

export const hexToHsv = (hex: string): Hsv => rgbToHsv(hexToRgb(hex))

export const hsvToHex = (hsv: Hsv): string => rgbToHex(hsvToRgb(hsv))
