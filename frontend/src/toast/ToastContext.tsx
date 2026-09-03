import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import Alert from '@mui/material/Alert'
import Snackbar from '@mui/material/Snackbar'

type Severita = 'success' | 'error'

interface Toast {
  chiave: number
  severita: Severita
  messaggio: string
}

interface ToastContextValue {
  /** Conferma di un'azione riuscita (es. "Impostazioni salvate.") — compare in alto e scompare da sola, non va confermata a mano. */
  successo: (messaggio: string) => void
  /** Esito negativo di un'azione tentata (es. "Operazione non riuscita, riprova.") — stesso comportamento transitorio, resta visibile più a lungo per dare tempo di leggerlo. */
  errore: (messaggio: string) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

const DURATA_SUCCESSO_MS = 4000
const DURATA_ERRORE_MS = 6000

/**
 * Notifiche transitorie stile "message.success/error" (antd) — sostituiscono gli Alert di
 * successo/errore che restavano incollati nel form finché non li chiudevi a mano. Coda semplice
 * (un Toast alla volta, il successivo compare alla chiusura del precedente): pattern ufficiale MUI
 * per Snackbar consecutivi, la sovrapposizione di più notifiche non è mai stata un caso reale qui.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [coda, setCoda] = useState<Toast[]>([])
  const [corrente, setCorrente] = useState<Toast | null>(null)
  const [aperto, setAperto] = useState(false)

  const accoda = useCallback((severita: Severita, messaggio: string) => {
    setCoda((precedente) => [...precedente, { chiave: Date.now() + Math.random(), severita, messaggio }])
  }, [])

  const successo = useCallback((messaggio: string) => accoda('success', messaggio), [accoda])
  const erroreToast = useCallback((messaggio: string) => accoda('error', messaggio), [accoda])

  // Pattern ufficiale MUI per Snackbar consecutivi: quando arriva un nuovo toast mentre uno è
  // ancora visibile, prima si chiude quello corrente (transizione di uscita), solo dopo — a
  // Snackbar già smontato (onExited) — si mostra il successivo dalla coda.
  useEffect(() => {
    if (coda.length > 0 && !corrente) {
      setCorrente(coda[0])
      setCoda((precedente) => precedente.slice(1))
      setAperto(true)
    } else if (coda.length > 0 && corrente && aperto) {
      setAperto(false)
    }
  }, [coda, corrente, aperto])

  function chiudi(_: unknown, motivo?: string) {
    if (motivo === 'clickaway') {
      return
    }
    setAperto(false)
  }

  return (
    <ToastContext.Provider value={{ successo, errore: erroreToast }}>
      {children}
      <Snackbar
        open={aperto}
        autoHideDuration={corrente?.severita === 'error' ? DURATA_ERRORE_MS : DURATA_SUCCESSO_MS}
        onClose={chiudi}
        anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
        slotProps={{ transition: { onExited: () => setCorrente(null) } }}
      >
        <Alert onClose={chiudi} severity={corrente?.severita} variant="filled" sx={{ boxShadow: 3 }}>
          {corrente?.messaggio}
        </Alert>
      </Snackbar>
    </ToastContext.Provider>
  )
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext)
  if (!ctx) {
    throw new Error('useToast deve essere usato dentro ToastProvider')
  }
  return ctx
}
