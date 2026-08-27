import { cancellaSessione, leggiSessione } from '../auth/tokenStorage'

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export const SESSIONE_SCADUTA_EVENT = 'gestisoft:sessione-scaduta'

export class ApiError extends Error {
  status: number
  dettagli: unknown

  constructor(status: number, message: string, dettagli?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.dettagli = dettagli
  }
}

async function apiRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const sessione = leggiSessione()

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(sessione ? { Authorization: `Bearer ${sessione.token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (response.status === 401) {
    cancellaSessione()
    window.dispatchEvent(new Event(SESSIONE_SCADUTA_EVENT))
    throw new ApiError(401, 'Sessione scaduta, effettua di nuovo l\'accesso.')
  }

  if (!response.ok) {
    const corpo = await response.json().catch(() => null)
    const messaggio =
      (corpo && typeof corpo === 'object' && 'title' in corpo && typeof corpo.title === 'string' && corpo.title) ||
      (corpo && typeof corpo === 'object' && 'message' in corpo && typeof corpo.message === 'string' && corpo.message) ||
      `Richiesta a ${path} fallita (${response.status})`
    throw new ApiError(response.status, messaggio, corpo)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const apiGet = <T>(path: string) => apiRequest<T>('GET', path)
export const apiPost = <T>(path: string, body?: unknown) => apiRequest<T>('POST', path, body)
export const apiPut = <T>(path: string, body?: unknown) => apiRequest<T>('PUT', path, body)
export const apiDelete = <T>(path: string) => apiRequest<T>('DELETE', path)

/** Scarica un file binario (PDF/XML) autenticato e avvia il download nel browser. */
export async function apiScaricaFile(path: string, nomeFile: string): Promise<void> {
  const sessione = leggiSessione()

  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: sessione ? { Authorization: `Bearer ${sessione.token}` } : {},
  })

  if (response.status === 401) {
    cancellaSessione()
    window.dispatchEvent(new Event(SESSIONE_SCADUTA_EVENT))
    throw new ApiError(401, 'Sessione scaduta, effettua di nuovo l\'accesso.')
  }

  if (!response.ok) {
    throw new ApiError(response.status, `Download di ${nomeFile} non riuscito (${response.status}).`)
  }

  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = nomeFile
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
