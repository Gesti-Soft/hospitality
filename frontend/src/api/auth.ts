import { apiGet, apiPost } from './client'

export interface LoginRequest {
  email: string
  password: string
  /** Token del browser già verificato in precedenza: se ancora valido, il codice non viene richiesto. */
  tokenDispositivo: string | null
}

export interface LoginResponse {
  token: string
  scadeAtUtc: string
  utenteId: string
  email: string
  isSuperAdmin: boolean
  clienteId: string | null
  isClienteAccount: boolean
}

/** Primo passo: o la sessione è pronta, o manca solo il codice — mai entrambi. */
export interface EsitoLoginResponse {
  richiede2Fa: boolean
  sessione: LoginResponse | null
  tokenVerifica2Fa: string | null
}

export interface Verifica2FaResponse {
  sessione: LoginResponse
  tokenDispositivo: string | null
  dispositivoScadeAtUtc: string | null
  codiciRecuperoRimasti: number
}

export interface Stato2FaResponse {
  attivo: boolean
  codiciRimasti: number
}

export interface Avvia2FaResponse {
  secret: string
  uriOtpauth: string
}

export const login = (request: LoginRequest) => apiPost<EsitoLoginResponse>('/auth/login', request)

export const verifica2Fa = (request: { tokenVerifica2Fa: string; codice: string; ricordaDispositivo: boolean }) =>
  apiPost<Verifica2FaResponse>('/auth/2fa/verifica', request)

export const stato2Fa = () => apiGet<Stato2FaResponse>('/auth/2fa')

export const avvia2Fa = () => apiPost<Avvia2FaResponse>('/auth/2fa/avvia', {})

export const attiva2Fa = (codice: string) => apiPost<{ codici: string[] }>('/auth/2fa/attiva', { codice })

export const disattiva2Fa = (password: string) => apiPost<void>('/auth/2fa/disattiva', { password })

export const rigeneraCodiciRecupero = (password: string) => apiPost<{ codici: string[] }>('/auth/2fa/codici-recupero', { password })
