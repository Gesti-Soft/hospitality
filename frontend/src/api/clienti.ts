import { useMutation, useQuery } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'

export interface ClienteDto {
  id: string
  ragioneSociale: string
  partitaIva: string | null
  attivo: boolean
  createdAtUtc: string
}

export interface CreaClienteRequest {
  ragioneSociale: string
  partitaIva: string | null
}

export function useClienti(abilitato: boolean) {
  return useQuery({
    queryKey: ['clienti'],
    queryFn: () => apiGet<ClienteDto[]>('/clienti'),
    enabled: abilitato,
  })
}

export function useCreaCliente() {
  return useMutation({
    mutationFn: (request: CreaClienteRequest) => apiPost<ClienteDto>('/clienti', request),
  })
}

export interface AggiornaClienteRequest {
  ragioneSociale: string
  partitaIva: string | null
}

export function useAggiornaCliente() {
  return useMutation({
    mutationFn: ({ clienteId, request }: { clienteId: string; request: AggiornaClienteRequest }) =>
      apiPut<ClienteDto>(`/clienti/${clienteId}`, request),
  })
}
