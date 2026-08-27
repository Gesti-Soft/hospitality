import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface EntrataDto {
  id: string
  strutturaId: string
  tipoEntrata: string | null
  nome: string | null
  importoEntrata: number
  descrizione: string | null
  data: string | null
  anno: number | null
}

export interface EntrataRequest {
  tipoEntrata: string | null
  nome: string | null
  importoEntrata: number
  descrizione: string | null
  data: string | null
}

export function useEntrate(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['entrate', strutturaId, anno],
    queryFn: () => apiGet<EntrataDto[]>(`/strutture/${strutturaId}/entrate?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

function useInvalidaEntrate(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['entrate', strutturaId] })
    queryClient.invalidateQueries({ queryKey: ['finanze', strutturaId, 'riepilogo-cassa'] })
  }
}

export function useCreaEntrata(strutturaId: string | null) {
  const invalida = useInvalidaEntrate(strutturaId)
  return useMutation({
    mutationFn: (request: EntrataRequest) => apiPost<EntrataDto>(`/strutture/${strutturaId}/entrate`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaEntrata(strutturaId: string | null) {
  const invalida = useInvalidaEntrate(strutturaId)
  return useMutation({
    mutationFn: ({ entrataId, request }: { entrataId: string; request: EntrataRequest }) =>
      apiPut<EntrataDto>(`/strutture/${strutturaId}/entrate/${entrataId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaEntrata(strutturaId: string | null) {
  const invalida = useInvalidaEntrate(strutturaId)
  return useMutation({
    mutationFn: (entrataId: string) => apiDelete<void>(`/strutture/${strutturaId}/entrate/${entrataId}`),
    onSuccess: invalida,
  })
}
