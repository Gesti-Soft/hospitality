import { cancellaSessione, leggiSessione, salvaSessione, type Sessione } from '../auth/tokenStorage'

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export const SESSIONE_SCADUTA_EVENT = 'gestisoft:sessione-scaduta'
export const SESSIONE_RINNOVATA_EVENT = 'gestisoft:sessione-rinnovata'

// Rinnova il token in background ad ogni richiesta autenticata andata a buon fine (non 401), con un
// throttle per non chiamare /auth/refresh ad ogni singola azione — così la sessione resta viva
// finché l'utente la usa davvero, e scade solo dopo un periodo di inattività reale pari alla durata
// del token (Jwt:LifetimeMinutes lato backend). Se il rinnovo fallisce non succede nulla di
// grave: la sessione scade semplicemente alla sua naturale scadenza.
const REFRESH_THROTTLE_MS = 5 * 60 * 1000
let ultimoRefreshMs = 0

function rinnovaSessioneSeNecessario(sessione: Sessione | null): void {
  if (!sessione) {
    return
  }

  const ora = Date.now()
  if (ora - ultimoRefreshMs < REFRESH_THROTTLE_MS) {
    return
  }
  ultimoRefreshMs = ora

  void fetch(`${API_BASE_URL}/auth/refresh`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${sessione.token}` },
  })
    .then((response) => (response.ok ? (response.json() as Promise<Sessione>) : null))
    .then((risposta) => {
      if (!risposta) {
        return
      }
      salvaSessione(risposta)
      window.dispatchEvent(new Event(SESSIONE_RINNOVATA_EVENT))
    })
    .catch(() => {
      // Silenzioso, vedi commento sopra.
    })
}

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

  // Un 401 significa "sessione scaduta" solo se avevamo davvero un token da mandare (richiesta
  // autenticata rifiutata) — senza sessione (es. /auth/login con credenziali sbagliate) è solo un
  // errore applicativo come un altro, il messaggio specifico arriva dal corpo della risposta più sotto.
  if (response.status === 401 && sessione) {
    cancellaSessione()
    window.dispatchEvent(new Event(SESSIONE_SCADUTA_EVENT))
    throw new ApiError(401, 'Sessione scaduta, effettua di nuovo l\'accesso.')
  }

  rinnovaSessioneSeNecessario(sessione)

  if (!response.ok) {
    const corpo = await response.json().catch(() => null)
    // "detail" (ProblemDetails, vedi GlobalExceptionHandler) porta sempre il messaggio specifico e
    // comprensibile ("Questa camera è già prenotata..."); "title" è solo l'etichetta generica della
    // categoria di errore ("Richiesta non valida") — va usato solo come ultima risorsa.
    const messaggio =
      (corpo && typeof corpo === 'object' && 'detail' in corpo && typeof corpo.detail === 'string' && corpo.detail) ||
      (corpo && typeof corpo === 'object' && 'message' in corpo && typeof corpo.message === 'string' && corpo.message) ||
      (corpo && typeof corpo === 'object' && 'title' in corpo && typeof corpo.title === 'string' && corpo.title) ||
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

  if (response.status === 401 && sessione) {
    cancellaSessione()
    window.dispatchEvent(new Event(SESSIONE_SCADUTA_EVENT))
    throw new ApiError(401, 'Sessione scaduta, effettua di nuovo l\'accesso.')
  }

  rinnovaSessioneSeNecessario(sessione)

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
