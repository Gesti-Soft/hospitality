import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'
import { confrontaNaturale } from '../lib/ordinamento'

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
  /** Null = come la struttura, 0 = nessuna, N = ogni N giorni. Si salva con useAggiornaPulizieTipologia, non con il form generale. */
  intervalloPuliziaGiorni: number | null
  intervalloBiancheriaGiorni: number | null
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
    // Ordinamento naturale applicato qui una sola volta: si riflette automaticamente in tutte le
    // select/autocomplete dell'app che elencano tipologie, senza doverlo ripetere pagina per pagina.
    select: (tipologie) => [...tipologie].sort((a, b) => confrontaNaturale(a.tipologiaCamera, b.tipologiaCamera)),
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

/**
 * Frequenze di pulizia e cambio biancheria della tipologia. Endpoint a parte e non dentro
 * TipologiaCameraRequest: quella la rimanda anche la pagina Servizi OTA con un elenco fisso di campi,
 * e un campo in più lì verrebbe azzerato a ogni suo salvataggio.
 */
export function useAggiornaPulizieTipologia(strutturaId: string | null) {
  const invalida = useInvalidaTipologie(strutturaId)
  return useMutation({
    mutationFn: ({ tipologiaId, intervalloPuliziaGiorni, intervalloBiancheriaGiorni }: { tipologiaId: string; intervalloPuliziaGiorni: number | null; intervalloBiancheriaGiorni: number | null }) =>
      apiPut<void>(`/strutture/${strutturaId}/tipologie-camera/${tipologiaId}/pulizie`, { intervalloPuliziaGiorni, intervalloBiancheriaGiorni }),
    onSuccess: invalida,
  })
}
