import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useClienti, type ClienteDto } from '../api/clienti'
import { useStrutture, type StrutturaDto } from '../api/strutture'
import { useAuth } from '../auth/AuthContext'
import {
  leggiSelezioneSuperAdmin,
  leggiStrutturaOperatore,
  salvaSelezioneSuperAdmin,
  salvaStrutturaOperatore,
} from './selezioneSalvata'

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

export function StrutturaProvider({ children }: { children: ReactNode }) {
  const { sessione } = useAuth()
  const isSuperAdmin = sessione?.isSuperAdmin ?? false

  // Il SuperAdmin non ha un Cliente/una Struttura propri: a ogni **accesso** entrambe le select
  // ripartono vuote. Un ricaricamento della pagina però non è un nuovo accesso — la scelta fatta
  // poco prima si riprende da dove l'ha lasciata, altrimenti chi aggiorna una pagina operativa
  // perde la struttura e viene rimbalzato sulla dashboard Super Admin (vedi selezioneSalvata).
  const [selezioneIniziale] = useState(() => (isSuperAdmin ? leggiSelezioneSuperAdmin() : null))
  const [clienteId, setClienteId] = useState<string | null>(selezioneIniziale?.clienteId ?? null)
  const [strutturaId, setStrutturaId] = useState<string | null>(() =>
    isSuperAdmin ? (selezioneIniziale?.strutturaId ?? null) : leggiStrutturaOperatore(),
  )

  // Un punto solo che rispecchia la scelta corrente nel browser, invece di ricordarsi di scriverla
  // in ognuno dei modi in cui può cambiare (scelta a mano, auto-selezione della prima struttura,
  // cliente o struttura spariti dalla lista).
  useEffect(() => {
    if (!isSuperAdmin) return
    salvaSelezioneSuperAdmin(clienteId, strutturaId)
  }, [isSuperAdmin, clienteId, strutturaId])

  const { data: clienti, isLoading: clientiLoading } = useClienti(isSuperAdmin)
  // Il SuperAdmin senza ancora un Cliente scelto non ha nessuna struttura sensata da elencare (la
  // select Struttura resta nascosta, vedi AppShell): non interrogare l'endpoint finché non sceglie.
  const { data: strutture, isLoading: struttureLoading } = useStrutture(isSuperAdmin ? clienteId : null, !isSuperAdmin || !!clienteId)

  // Se il Cliente scelto sparisce dalla lista (es. eliminato) durante la sessione, azzera la
  // selezione invece di sceglierne un altro a caso.
  useEffect(() => {
    if (!isSuperAdmin || !clienti || clienti.length === 0 || !clienteId) return
    if (clienti.some((c) => c.id === clienteId)) return
    setClienteId(null)
    // Anche la struttura: appartiene a quel cliente, e da sola non sarebbe più risolvibile — ora
    // che la selezione sopravvive al ricaricamento, lasciarla indietro la renderebbe permanente.
    setStrutturaId(null)
  }, [isSuperAdmin, clienti, clienteId])

  // Quando la lista strutture cambia, assicurati che la struttura selezionata sia una di quelle
  // disponibili — altrimenti scegli la prima. Per il SuperAdmin la query è disabilitata finché non
  // sceglie un Cliente (vedi sopra), quindi qui non scatta nulla fino a quel momento.
  useEffect(() => {
    if (!strutture) return // query ancora in caricamento, non toccare la selezione salvata
    if (strutture.length === 0) {
      if (strutturaId) {
        setStrutturaId(null)
        if (!isSuperAdmin) salvaStrutturaOperatore(null)
      }
      return
    }
    if (strutturaId && strutture.some((s) => s.id === strutturaId)) return
    if (isSuperAdmin && !clienteId) return
    setStrutturaId(strutture[0].id)
    if (!isSuperAdmin) salvaStrutturaOperatore(strutture[0].id)
  }, [strutture, strutturaId, isSuperAdmin, clienteId])

  const selezionaCliente = (id: string) => {
    setClienteId(id)
    setStrutturaId(null)
  }

  const selezionaStruttura = (id: string) => {
    setStrutturaId(id)
    if (!isSuperAdmin) salvaStrutturaOperatore(id)
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
