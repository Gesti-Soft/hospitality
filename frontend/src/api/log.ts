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
export const CATEGORIE_LOG = ['Prenotazione', 'Utente', 'Servizi', 'Wubook', 'AlloggiatiWeb', 'Osservatorio', 'PayTourist', 'Auth', 'SuperAdmin', 'Backup'] as const

/**
 * Sottoinsieme visibile a un Cliente — deve restare identico a LogVisibilita.CategorieVisibiliCliente
 * lato backend. Niente "Auth": il log di un Cliente mostra solo la struttura selezionata e le righe
 * di login non ne hanno una, quindi quel filtro non troverebbe mai nulla.
 */
export const CATEGORIE_LOG_CLIENTE = ['Prenotazione', 'Utente', 'Servizi', 'AlloggiatiWeb', 'Osservatorio', 'PayTourist'] as const

/**
 * Come mostrare una categoria all'utente. La categoria è l'identificatore con cui la riga è salvata
 * — quello non si tocca, altrimenti le righe già scritte non sarebbero più filtrabili insieme alle
 * nuove — ma il nome del fornitore del channel manager non deve comparire a schermo: si legge
 * "OTA". Tradurre qui invece di rinominare a database sistema anche tutto lo storico.
 */
const ETICHETTE_CATEGORIA: Record<string, string> = { Wubook: 'OTA' }

export const etichettaCategoria = (categoria: string | null | undefined): string =>
  categoria == null ? '' : (ETICHETTE_CATEGORIA[categoria] ?? categoria)

// Scroll infinito (stesso principio di Ospiti/Arrivi-InCorso-Storico, ma qui la paginazione è
// server-side, non un semplice slice client-side di dati già tutti caricati): parte da 25 righe,
// ne carica altre 25 via fetchNextPage quando la sentinella in fondo alla tabella entra in vista.
export function useLogs(
  strutturaId: string | null,
  livello: LivelloLog | null,
  categoria: string | null,
  ricerca: string,
  da: string,
  a: string,
  pageSize: number,
  /** Restringe la ricerca a queste categorie (la pagina Accessi e sicurezza usa solo quelle sue). */
  categorieAmmesse?: readonly string[],
) {
  return useInfiniteQuery({
    queryKey: ['logs', strutturaId, livello, categoria, ricerca, da, a, pageSize, categorieAmmesse],
    initialPageParam: 1,
    queryFn: ({ pageParam }) => {
      const parametri = new URLSearchParams({ page: String(pageParam), pageSize: String(pageSize) })
      if (strutturaId) parametri.set('strutturaId', strutturaId)
      categorieAmmesse?.forEach((c) => parametri.append('categorie', c))
      if (livello != null) parametri.set('livello', String(livello))
      if (categoria) parametri.set('categoria', categoria)
      if (ricerca.trim()) parametri.set('ricerca', ricerca.trim())
      if (da) parametri.set('da', da)
      if (a) parametri.set('a', a)
      return apiGet<PagedResultDto<LogEventoDto>>(`/logs?${parametri.toString()}`)
    },
    getNextPageParam: (lastPage, allPages) => {
      const caricati = allPages.reduce((totale, p) => totale + p.items.length, 0)
      return caricati < lastPage.totalCount ? allPages.length + 1 : undefined
    },
  })
}
