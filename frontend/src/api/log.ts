import { useInfiniteQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export const LivelloLog = { Info: 1, Warning: 2, Error: 3 } as const
export type LivelloLog = (typeof LivelloLog)[keyof typeof LivelloLog]

export interface LogEventoDto {
  id: string
  clienteId: string | null
  strutturaId: string | null
  livello: LivelloLog
  messaggio: string
  dettaglio: string | null
  correlationId: string | null
  origine: string
  createdAtUtc: string
  /** Es. "Prenotazione", "Utente", "Servizi", "Wubook", "AlloggiatiWeb", "Osservatorio", "PayTourist", "Auth", "SuperAdmin" — null per gli errori di sistema generici. */
  categoria: string | null
  /** Email dell'utente che ha compiuto l'azione — null per eventi di sistema/job automatici. */
  operatore: string | null
}

export interface PagedResultDto<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

/** Tutte le categorie — solo per il Super Admin (vedi LogVisibilita lato backend, applicata comunque anche se il filtro qui non venisse rispettato). */
export const CATEGORIE_LOG = ['Prenotazione', 'Utente', 'Servizi', 'Wubook', 'AlloggiatiWeb', 'Osservatorio', 'PayTourist', 'Auth', 'SuperAdmin'] as const

/**
 * Sottoinsieme visibile a un Cliente — deve restare identico a LogVisibilita.CategorieVisibiliCliente
 * lato backend, con l'aggiunta di "Auth": non è nella whitelist backend (un Cliente non vede login
 * altrui né del Super Admin), ma il backend lascia comunque passare le PROPRIE righe di login — qui
 * serve solo a offrire il filtro nel menu a tendina.
 */
export const CATEGORIE_LOG_CLIENTE = ['Prenotazione', 'Utente', 'Servizi', 'AlloggiatiWeb', 'Osservatorio', 'PayTourist', 'Auth'] as const

// Scroll infinito (stesso principio di Ospiti/Arrivi-InCorso-Storico, ma qui la paginazione è
// server-side, non un semplice slice client-side di dati già tutti caricati): parte da 25 righe,
// ne carica altre 25 via fetchNextPage quando la sentinella in fondo alla tabella entra in vista.
export function useLogs(strutturaId: string | null, livello: LivelloLog | null, categoria: string | null, pageSize: number) {
  return useInfiniteQuery({
    queryKey: ['logs', strutturaId, livello, categoria, pageSize],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => {
      const parametri = new URLSearchParams({ page: String(pageParam), pageSize: String(pageSize) })
      if (strutturaId) parametri.set('strutturaId', strutturaId)
      if (livello != null) parametri.set('livello', String(livello))
      if (categoria) parametri.set('categoria', categoria)
      return apiGet<PagedResultDto<LogEventoDto>>(`/logs?${parametri.toString()}`)
    },
    getNextPageParam: (lastPage, allPages) => {
      const caricati = allPages.reduce((totale, p) => totale + p.items.length, 0)
      return caricati < lastPage.totalCount ? allPages.length + 1 : undefined
    },
  })
}
