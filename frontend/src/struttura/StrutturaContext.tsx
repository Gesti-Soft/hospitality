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

const CHIAVE_CLIENTE = 'gestisoft.clienteId'
const CHIAVE_STRUTTURA = 'gestisoft.strutturaId'

export function StrutturaProvider({ children }: { children: ReactNode }) {
  const { sessione } = useAuth()
  const isSuperAdmin = sessione?.isSuperAdmin ?? false

  const [clienteId, setClienteId] = useState<string | null>(() => (isSuperAdmin ? localStorage.getItem(CHIAVE_CLIENTE) : null))
  const [strutturaId, setStrutturaId] = useState<string | null>(() => localStorage.getItem(CHIAVE_STRUTTURA))

  const { data: clienti, isLoading: clientiLoading } = useClienti(isSuperAdmin)
  const { data: strutture, isLoading: struttureLoading } = useStrutture(isSuperAdmin ? clienteId : null)

  // SuperAdmin: se non c'è ancora un Cliente selezionato (o quello salvato non esiste più), scegli il primo disponibile.
  useEffect(() => {
    if (!isSuperAdmin || !clienti || clienti.length === 0) return
    if (clienteId && clienti.some((c) => c.id === clienteId)) return
    setClienteId(clienti[0].id)
  }, [isSuperAdmin, clienti, clienteId])

  // Quando la lista strutture (dipendente dal Cliente per il SuperAdmin) cambia, assicurati che la
  // struttura selezionata sia una di quelle disponibili — altrimenti scegli la prima.
  useEffect(() => {
    if (!strutture || strutture.length === 0) return
    if (strutturaId && strutture.some((s) => s.id === strutturaId)) return
    setStrutturaId(strutture[0].id)
  }, [strutture, strutturaId])

  const selezionaCliente = (id: string) => {
    setClienteId(id)
    localStorage.setItem(CHIAVE_CLIENTE, id)
    setStrutturaId(null)
    localStorage.removeItem(CHIAVE_STRUTTURA)
  }

  const selezionaStruttura = (id: string) => {
    setStrutturaId(id)
    localStorage.setItem(CHIAVE_STRUTTURA, id)
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
