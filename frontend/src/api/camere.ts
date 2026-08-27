import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

// L'Api non registra JsonStringEnumConverter, quindi gli enum arrivano sul wire come numeri
// (valori esatti di GestiSoft.Domain.Enums.StatoCamera), non come stringhe.
export const StatoCamera = { Pronta: 1, Occupata: 2, DaPulire: 3, NonDisponibile: 4 } as const
export type StatoCamera = (typeof StatoCamera)[keyof typeof StatoCamera]

export interface CameraDto {
  id: string
  strutturaId: string
  tipologiaId: string | null
  tipologiaNome: string | null
  stateRoom: StatoCamera
  nome: string
  capacitaOspiti: number | null
  soggiornoMinimo: number | null
  idCameraWubook: number | null
  wubookAttiva: boolean
}

export interface CameraRequest {
  tipologiaId: string | null
  stateRoom: StatoCamera
  nome: string
  capacitaOspiti: number | null
  soggiornoMinimo: number | null
}

export function useCamere(strutturaId: string | null) {
  return useQuery({
    queryKey: ['camere', strutturaId],
    queryFn: () => apiGet<CameraDto[]>(`/strutture/${strutturaId}/camere`),
    enabled: !!strutturaId,
  })
}

function useInvalidaCamere(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] })
}

export function useCreaCamera(strutturaId: string | null) {
  const invalida = useInvalidaCamere(strutturaId)
  return useMutation({
    mutationFn: (request: CameraRequest) => apiPost<CameraDto>(`/strutture/${strutturaId}/camere`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaCamera(strutturaId: string | null) {
  const invalida = useInvalidaCamere(strutturaId)
  return useMutation({
    mutationFn: ({ cameraId, request }: { cameraId: string; request: CameraRequest }) =>
      apiPut<CameraDto>(`/strutture/${strutturaId}/camere/${cameraId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaCamera(strutturaId: string | null) {
  const invalida = useInvalidaCamere(strutturaId)
  return useMutation({
    mutationFn: (cameraId: string) => apiDelete<void>(`/strutture/${strutturaId}/camere/${cameraId}`),
    onSuccess: invalida,
  })
}
