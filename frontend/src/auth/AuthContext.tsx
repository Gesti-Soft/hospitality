import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { login as loginRequest, verifica2Fa as verifica2FaRequest, type LoginResponse } from '../api/auth'
import { ApiError } from '../api/client'
import { SESSIONE_RINNOVATA_EVENT, SESSIONE_SCADUTA_EVENT } from '../api/client'
import {
  cancellaSessione,
  leggiSessione,
  leggiTokenDispositivo,
  salvaSessione,
  salvaTokenDispositivo,
  type Sessione,
} from './tokenStorage'

interface AuthContextValue {
  sessione: Sessione | null
  loading: boolean
  errore: string | null
  /** Risolve a true quando manca solo il codice di verifica: la pagina di login passa al secondo passo. */
  accedi: (email: string, password: string) => Promise<boolean>
  /** Secondo passo, valido solo dopo un accedi() che ha chiesto il codice. */
  completaVerifica2Fa: (codice: string, ricordaDispositivo: boolean) => Promise<void>
  /** Annulla il secondo passo e torna a email e password. */
  annullaVerifica2Fa: () => void
  richiede2Fa: boolean
  esci: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sessione, setSessione] = useState<Sessione | null>(() => leggiSessione())
  const [loading, setLoading] = useState(false)
  const [errore, setErrore] = useState<string | null>(null)
  // Dura quanto il passaggio dal form password al form codice: vale pochi minuti e non apre nulla
  // da solo, quindi resta in memoria e non in localStorage — chiudendo la pagina si ricomincia.
  const [tokenVerifica2Fa, setTokenVerifica2Fa] = useState<string | null>(null)

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

  const apriSessione = (risposta: LoginResponse) => {
    const nuovaSessione: Sessione = {
      token: risposta.token,
      scadeAtUtc: risposta.scadeAtUtc,
      utenteId: risposta.utenteId,
      email: risposta.email,
      isSuperAdmin: risposta.isSuperAdmin,
      clienteId: risposta.clienteId,
      isClienteAccount: risposta.isClienteAccount,
    }
    salvaSessione(nuovaSessione)
    setSessione(nuovaSessione)
  }

  const accedi = async (email: string, password: string): Promise<boolean> => {
    setLoading(true)
    setErrore(null)
    try {
      const risposta = await loginRequest({ email, password, tokenDispositivo: leggiTokenDispositivo() })

      if (risposta.richiede2Fa && risposta.tokenVerifica2Fa) {
        setTokenVerifica2Fa(risposta.tokenVerifica2Fa)
        return true
      }

      if (risposta.sessione) {
        apriSessione(risposta.sessione)
      }
      return false
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Impossibile contattare il server.')
      throw err
    } finally {
      setLoading(false)
    }
  }

  const completaVerifica2Fa = async (codice: string, ricordaDispositivo: boolean) => {
    if (!tokenVerifica2Fa) {
      setErrore('Verifica scaduta: rifai il login.')
      throw new Error('token di verifica assente')
    }

    setLoading(true)
    setErrore(null)
    try {
      const risposta = await verifica2FaRequest({ tokenVerifica2Fa, codice, ricordaDispositivo })
      if (risposta.tokenDispositivo) {
        salvaTokenDispositivo(risposta.tokenDispositivo)
      }
      setTokenVerifica2Fa(null)
      apriSessione(risposta.sessione)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Impossibile contattare il server.')
      throw err
    } finally {
      setLoading(false)
    }
  }

  const annullaVerifica2Fa = () => {
    setTokenVerifica2Fa(null)
    setErrore(null)
  }

  const esci = () => {
    // Il token del dispositivo NON si cancella: è il browser a essere già stato verificato, e
    // cancellarlo obbligherebbe a rifare il codice a ogni uscita, svuotando di senso i 7 giorni.
    cancellaSessione()
    setTokenVerifica2Fa(null)
    setSessione(null)
  }

  return (
    <AuthContext.Provider
      value={{
        sessione,
        loading,
        errore,
        accedi,
        completaVerifica2Fa,
        annullaVerifica2Fa,
        richiede2Fa: tokenVerifica2Fa !== null,
        esci,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth deve essere usato dentro AuthProvider')
  }
  return ctx
}
