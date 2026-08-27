import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost } from './client'

export interface PrezzoCameraDto {
  id: string
  strutturaId: string
  cameraId: string | null
  tipologiaId: string | null
  dataInizio: string | null
  dataFine: string | null
  prezzoPerNotte: number | null
}

export interface ImpostaPrezzoRequest {
  cameraId: string | null
  tipologiaId: string | null
  dataInizio: string
  dataFine: string
  prezzoPerNotte: number
}

export function usePrezzi(strutturaId: string | null) {
  return useQuery({
    queryKey: ['prezzi-camera', strutturaId],
    queryFn: () => apiGet<PrezzoCameraDto[]>(`/strutture/${strutturaId}/prezzi-camera`),
    enabled: !!strutturaId,
  })
}

function useInvalidaPrezzi(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['prezzi-camera', strutturaId] })
}

export function useImpostaPrezzo(strutturaId: string | null) {
  const invalida = useInvalidaPrezzi(strutturaId)
  return useMutation({
    mutationFn: (request: ImpostaPrezzoRequest) => apiPost<PrezzoCameraDto>(`/strutture/${strutturaId}/prezzi-camera`, request),
    onSuccess: invalida,
  })
}

export function useEliminaPrezzo(strutturaId: string | null) {
  const invalida = useInvalidaPrezzi(strutturaId)
  return useMutation({
    mutationFn: (prezzoId: string) => apiDelete<void>(`/strutture/${strutturaId}/prezzi-camera/${prezzoId}`),
    onSuccess: invalida,
  })
}
