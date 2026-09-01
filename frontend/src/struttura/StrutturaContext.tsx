import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useClienti, type ClienteDto } from '../api/clienti'
import { useStrutture, type StrutturaDto } from '../api/strutture'
import { useAuth } from '../auth/AuthContext'

interface StrutturaContextValue {
  isSuperAdmin: boolean
  clienti: ClienteDto[]
  clienteId: string | null
  strutture: StrutturaDto[]
  strutturaId: string | null
  strutturaCorrente: StrutturaDto | null
  loading: boolean
  selezionaCliente: (id: string) => void
  selezionaStruttura: (id: string) => void
}

const StrutturaContext = createContext<StrutturaContextValue | null>(null)

// Solo per il Cliente/Operatore, che ha una sola struttura "propria" da ricordare tra un accesso e
// l'altro — il SuperAdmin naviga tra Clienti/Strutture diversi ad ogni accesso, non ha nulla da
// persistere.
const CHIAVE_STRUTTURA = 'gestisoft.strutturaId'

export function StrutturaProvider({ children }: { children: ReactNode }) {
  const { sessione } = useAuth()
  const isSuperAdmin = sessione?.isSuperAdmin ?? false

  // Il SuperAdmin non ha un Cliente/una Struttura propri: a ogni accesso entrambe le select
  // ripartono vuote (nessuna preselezione, nemmeno da una scelta precedente salvata) finché non ne
  // sceglie uno esplicitamente.
  const [clienteId, setClienteId] = useState<string | null>(null)
  const [strutturaId, setStrutturaId] = useState<string | null>(() => (isSuperAdmin ? null : localStorage.getItem(CHIAVE_STRUTTURA)))

  const { data: clienti, isLoading: clientiLoading } = useClienti(isSuperAdmin)
  const { data: strutture, isLoading: struttureLoading } = useStrutture(isSuperAdmin ? clienteId : null)

  // Se il Cliente scelto sparisce dalla lista (es. eliminato) durante la sessione, azzera la
  // selezione invece di sceglierne un altro a caso.
  useEffect(() => {
    if (!isSuperAdmin || !clienti || clienti.length === 0 || !clienteId) return
    if (clienti.some((c) => c.id === clienteId)) return
    setClienteId(null)
  }, [isSuperAdmin, clienti, clienteId])

  // Quando la lista strutture cambia, assicurati che la struttura selezionata sia una di quelle
  // disponibili — altrimenti scegli la prima. Per il SuperAdmin senza un Cliente scelto la select
  // Struttura mostra comunque tutte le strutture di tutti i Clienti (nessuna nascosta, vedi
  // useStrutture sopra), ma senza selezionarne una di default: l'auto-selezione scatta solo dopo
  // aver scelto esplicitamente un Cliente, sulla prima struttura di quel Cliente.
  useEffect(() => {
    if (!strutture) return // query ancora in caricamento, non toccare la selezione salvata
    if (strutture.length === 0) {
      if (strutturaId) {
        setStrutturaId(null)
        if (!isSuperAdmin) localStorage.removeItem(CHIAVE_STRUTTURA)
      }
      return
    }
    if (strutturaId && strutture.some((s) => s.id === strutturaId)) return
    if (isSuperAdmin && !clienteId) return
    setStrutturaId(strutture[0].id)
    if (!isSuperAdmin) localStorage.setItem(CHIAVE_STRUTTURA, strutture[0].id)
  }, [strutture, strutturaId, isSuperAdmin, clienteId])

  const selezionaCliente = (id: string) => {
    setClienteId(id)
    setStrutturaId(null)
  }

  const selezionaStruttura = (id: string) => {
    setStrutturaId(id)
    if (!isSuperAdmin) localStorage.setItem(CHIAVE_STRUTTURA, id)
  }

  const strutturaCorrente = strutture?.find((s) => s.id === strutturaId) ?? null

  return (
    <StrutturaContext.Provider
      value={{
        isSuperAdmin,
        clienti: clienti ?? [],
        clienteId,
        strutture: strutture ?? [],
        strutturaId,
        strutturaCorrente,
        loading: clientiLoading || struttureLoading,
        selezionaCliente,
        selezionaStruttura,
      }}
    >
      {children}
    </StrutturaContext.Provider>
  )
}

export function useStruttura(): StrutturaContextValue {
  const ctx = useContext(StrutturaContext)
  if (!ctx) {
    throw new Error('useStruttura deve essere usato dentro StrutturaProvider')
  }
  return ctx
}
