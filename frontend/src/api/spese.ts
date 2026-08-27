import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface SpesaDto {
  id: string
  strutturaId: string
  tipoSpesa: string | null
  nome: string | null
  importoSpesa: number
  descrizione: string | null
  metodoPagamento: string | null
  dataSpesa: string | null
  anno: number | null
}

export interface SpesaRequest {
  tipoSpesa: string | null
  nome: string | null
  importoSpesa: number
  descrizione: string | null
  metodoPagamento: string | null
  dataSpesa: string | null
}

export function useSpese(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['spese', strutturaId, anno],
    queryFn: () => apiGet<SpesaDto[]>(`/strutture/${strutturaId}/spese?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

function useInvalidaSpese(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['spese', strutturaId] })
    queryClient.invalidateQueries({ queryKey: ['finanze', strutturaId, 'riepilogo-cassa'] })
  }
}

export function useCreaSpesa(strutturaId: string | null) {
  const invalida = useInvalidaSpese(strutturaId)
  return useMutation({
    mutationFn: (request: SpesaRequest) => apiPost<SpesaDto>(`/strutture/${strutturaId}/spese`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaSpesa(strutturaId: string | null) {
  const invalida = useInvalidaSpese(strutturaId)
  return useMutation({
    mutationFn: ({ spesaId, request }: { spesaId: string; request: SpesaRequest }) =>
      apiPut<SpesaDto>(`/strutture/${strutturaId}/spese/${spesaId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaSpesa(strutturaId: string | null) {
  const invalida = useInvalidaSpese(strutturaId)
  return useMutation({
    mutationFn: (spesaId: string) => apiDelete<void>(`/strutture/${strutturaId}/spese/${spesaId}`),
    onSuccess: invalida,
  })
}
