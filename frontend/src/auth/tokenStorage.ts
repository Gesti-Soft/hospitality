export interface Sessione {
  token: string
  scadeAtUtc: string
  utenteId: string
  email: string
  isSuperAdmin: boolean
  clienteId: string | null
  isClienteAccount: boolean
}

const STORAGE_KEY = 'gestisoft.sessione'

/**
 * Token del "ricorda questo dispositivo" del 2FA. Vive separato dalla sessione perché deve
 * sopravvivere al logout: è il browser a essere già stato verificato, non la singola sessione.
 */
const CHIAVE_DISPOSITIVO = 'gestisoft.dispositivo2fa'

export function leggiSessione(): Sessione | null {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return null

  try {
    return JSON.parse(raw) as Sessione
  } catch {
    localStorage.removeItem(STORAGE_KEY)
    return null
  }
}

export function salvaSessione(sessione: Sessione): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessione))
}

export function cancellaSessione(): void {
  localStorage.removeItem(STORAGE_KEY)
}

export function leggiTokenDispositivo(): string | null {
  return localStorage.getItem(CHIAVE_DISPOSITIVO)
}

export function salvaTokenDispositivo(token: string): void {
  localStorage.setItem(CHIAVE_DISPOSITIVO, token)
}

export function cancellaTokenDispositivo(): void {
  localStorage.removeItem(CHIAVE_DISPOSITIVO)
}
