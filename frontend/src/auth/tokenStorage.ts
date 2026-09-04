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
