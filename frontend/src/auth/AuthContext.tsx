import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { login as loginRequest } from '../api/auth'
import { ApiError } from '../api/client'
import { SESSIONE_RINNOVATA_EVENT, SESSIONE_SCADUTA_EVENT } from '../api/client'
import { cancellaSessione, leggiSessione, salvaSessione, type Sessione } from './tokenStorage'

interface AuthContextValue {
  sessione: Sessione | null
  loading: boolean
  errore: string | null
  accedi: (email: string, password: string) => Promise<void>
  esci: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sessione, setSessione] = useState<Sessione | null>(() => leggiSessione())
  const [loading, setLoading] = useState(false)
  const [errore, setErrore] = useState<string | null>(null)

  useEffect(() => {
    const onSessioneScaduta = () => setSessione(null)
    const onSessioneRinnovata = () => setSessione(leggiSessione())
    window.addEventListener(SESSIONE_SCADUTA_EVENT, onSessioneScaduta)
    window.addEventListener(SESSIONE_RINNOVATA_EVENT, onSessioneRinnovata)
    return () => {
      window.removeEventListener(SESSIONE_SCADUTA_EVENT, onSessioneScaduta)
      window.removeEventListener(SESSIONE_RINNOVATA_EVENT, onSessioneRinnovata)
    }
  }, [])

  const accedi = async (email: string, password: string) => {
    setLoading(true)
    setErrore(null)
    try {
      const risposta = await loginRequest({ email, password })
      const nuovaSessione: Sessione = {
        token: risposta.token,
        scadeAtUtc: risposta.scadeAtUtc,
        utenteId: risposta.utenteId,
        email: risposta.email,
        isSuperAdmin: risposta.isSuperAdmin,
        clienteId: risposta.clienteId,
      }
      salvaSessione(nuovaSessione)
      setSessione(nuovaSessione)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Impossibile contattare il server.')
      throw err
    } finally {
      setLoading(false)
    }
  }

  const esci = () => {
    cancellaSessione()
    setSessione(null)
  }

  return <AuthContext.Provider value={{ sessione, loading, errore, accedi, esci }}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth deve essere usato dentro AuthProvider')
  }
  return ctx
}
