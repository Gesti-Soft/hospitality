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

/** Segna una camera come pulita (torna "Pronta") — pagina Pulizie. */
export function useSegnaCameraPulita(strutturaId: string | null) {
  const invalida = useInvalidaCamere(strutturaId)
  return useMutation({
    mutationFn: (cameraId: string) => apiPut<CameraDto>(`/strutture/${strutturaId}/camere/${cameraId}/pulita`),
    onSuccess: invalida,
  })
}

export interface RisultatoDuplicazioneCamereDto {
  tipologie: number
  camere: number
  prezzi: number
  canali: number
  saltati: number
}

/** Duplica tipologie/camere/prezzi/canali vendita da un'altra Struttura attiva dello stesso Cliente. */
export function useDuplicaCamere(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (strutturaOrigineId: string) =>
      apiPost<RisultatoDuplicazioneCamereDto>(`/strutture/${strutturaId}/camere/duplica-da/${strutturaOrigineId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['prezzi-camera', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['canali-vendita', strutturaId] })
    },
  })
}
