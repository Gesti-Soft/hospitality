import { useQuery } from '@tanstack/react-query'
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
}

export interface PagedResultDto<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function useLogs(strutturaId: string | null, livello: LivelloLog | null, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['logs', strutturaId, livello, page, pageSize],
    queryFn: () => {
      const parametri = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (strutturaId) parametri.set('strutturaId', strutturaId)
      if (livello != null) parametri.set('livello', String(livello))
      return apiGet<PagedResultDto<LogEventoDto>>(`/logs?${parametri.toString()}`)
    },
  })
}
