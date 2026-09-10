import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface TipologiaCameraDto {
  id: string
  strutturaId: string
  tipologiaCamera: string
  spesePulizia: number | null
  animali: number | null
  cauzione: number | null
  prezzoDefault: number | null
  numeroImplementoPersona: number
  implemento: number
  idCameraWubook: number | null
  wubookAttiva: boolean
  codiceCameraWubook: string | null
  wubookSoloWoodoo: boolean
}

export interface TipologiaCameraRequest {
  tipologiaCamera: string
  spesePulizia: number | null
  animali: number | null
  cauzione: number | null
  prezzoDefault: number | null
  numeroImplementoPersona: number
  implemento: number
  /** Impostazioni OTA per l'intero pool — editabili solo dal dialog camera della pagina Servizi OTA, mai da qui: sempre da passare invariate per non azzerarle. */
  codiceCameraWubook: string | null
  wubookSoloWoodoo: boolean
}

export function useTipologie(strutturaId: string | null) {
  return useQuery({
    queryKey: ['tipologie-camera', strutturaId],
    queryFn: () => apiGet<TipologiaCameraDto[]>(`/strutture/${strutturaId}/tipologie-camera`),
    enabled: !!strutturaId,
  })
}

function useInvalidaTipologie(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
    // Le camere espongono tipologiaNome in join: una modifica al nome tipologia va riflessa anche lì.
    queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] })
  }
}

export function useCreaTipologia(strutturaId: string | null) {
  const invalida = useInvalidaTipologie(strutturaId)
  return useMutation({
    mutationFn: (request: TipologiaCameraRequest) => apiPost<TipologiaCameraDto>(`/strutture/${strutturaId}/tipologie-camera`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaTipologia(strutturaId: string | null) {
  const invalida = useInvalidaTipologie(strutturaId)
  return useMutation({
    mutationFn: ({ tipologiaId, request }: { tipologiaId: string; request: TipologiaCameraRequest }) =>
      apiPut<TipologiaCameraDto>(`/strutture/${strutturaId}/tipologie-camera/${tipologiaId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaTipologia(strutturaId: string | null) {
  const invalida = useInvalidaTipologie(strutturaId)
  return useMutation({
    mutationFn: (tipologiaId: string) => apiDelete<void>(`/strutture/${strutturaId}/tipologie-camera/${tipologiaId}`),
    onSuccess: invalida,
  })
}
