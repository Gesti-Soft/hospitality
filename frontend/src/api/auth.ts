import { apiPost } from './client'

export interface LoginRequest {
  email: string
  password: string
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

export const login = (request: LoginRequest) => apiPost<LoginResponse>('/auth/login', request)
