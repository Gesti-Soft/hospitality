/**
 * Alleggerisce un'immagine prima di caricarla: in fattura il logo viene stampato alto 45pt, quindi
 * oltre una certa dimensione i pixel in piu' non si vedono, occupano spazio nel database e viaggiano
 * ad ogni sincronizzazione. Il ridimensionamento avviene qui e non sul server perche' il browser ha
 * gia' il decodificatore di immagini: nessuna libreria da aggiungere da nessuna delle due parti.
 */

/** Tre volte l'altezza di stampa: nitido anche su carta, senza portarsi dietro una foto intera. */
const ALTEZZA_MASSIMA_PX = 240

/** Oltre questa larghezza un logo molto orizzontale verrebbe comunque rimpicciolito dal PDF. */
const LARGHEZZA_MASSIMA_PX = 960

export const LOGO_MAX_BYTE = 512 * 1024
export const LOGO_TIPI_ACCETTATI = ['image/png', 'image/jpeg']

/**
 * Restituisce il file da inviare: ridimensionato se serve, altrimenti l'originale. Il PNG resta PNG
 * (la trasparenza di un logo va conservata) e il JPEG resta JPEG. Se il browser non riesce a
 * decodificare l'immagine si tiene l'originale: a rifiutarla, con un messaggio comprensibile, ci
 * pensa comunque il server.
 */
export async function alleggerisciLogo(file: File): Promise<File> {
  const bitmap = await leggiImmagine(file)
  if (!bitmap) {
    return file
  }

  const scala = Math.min(ALTEZZA_MASSIMA_PX / bitmap.height, LARGHEZZA_MASSIMA_PX / bitmap.width, 1)
  if (scala === 1 && file.size <= LOGO_MAX_BYTE) {
    bitmap.close?.()
    return file
  }

  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, Math.round(bitmap.width * scala))
  canvas.height = Math.max(1, Math.round(bitmap.height * scala))

  const contesto = canvas.getContext('2d')
  if (!contesto) {
    bitmap.close?.()
    return file
  }

  contesto.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close?.()

  const tipo = file.type === 'image/jpeg' ? 'image/jpeg' : 'image/png'
  const blob = await new Promise<Blob | null>((risolvi) => canvas.toBlob(risolvi, tipo, 0.9))

  // Su un logo gia' piccolo e ben compresso la riscrittura puo' pesare piu' dell'originale: in quel
  // caso il file di partenza e' la scelta migliore.
  if (!blob || blob.size >= file.size) {
    return file
  }

  const estensione = tipo === 'image/jpeg' ? 'jpg' : 'png'
  return new File([blob], `logo.${estensione}`, { type: tipo })
}

interface ImmagineDecodificata {
  width: number
  height: number
  close?: () => void
}

async function leggiImmagine(file: File): Promise<(CanvasImageSource & ImmagineDecodificata) | null> {
  if ('createImageBitmap' in window) {
    try {
      return await createImageBitmap(file)
    } catch {
      return null
    }
  }

  const url = URL.createObjectURL(file)
  try {
    return await new Promise((risolvi, rifiuta) => {
      const img = new Image()
      img.onload = () => risolvi(Object.assign(img, { close: () => URL.revokeObjectURL(url) }))
      img.onerror = () => rifiuta(new Error('immagine non leggibile'))
      img.src = url
    })
  } catch {
    URL.revokeObjectURL(url)
    return null
  }
}
