import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface ClienteDto {
  id: string
  ragioneSociale: string
  partitaIva: string | null
  attivo: boolean
  createdAtUtc: string
}

export function useClienti(abilitato: boolean) {
  return useQuery({
    queryKey: ['clienti'],
    queryFn: () => apiGet<ClienteDto[]>('/clienti'),
    enabled: abilitato,
  })
}
