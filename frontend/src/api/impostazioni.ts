import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut } from './client'

export interface ImpostazioniStrutturaDto {
  strutturaId: string
  poliziaStatoAttiva: boolean
  osservatorioAttivo: boolean
  payTouristAttivo: boolean
  oraInvioGiornaliero: string | null
  tassaSoggiornoPrezzo: number | null
  tassaSoggiornoMaxGiorni: number | null
}

export type ImpostazioniStrutturaRequest = Omit<ImpostazioniStrutturaDto, 'strutturaId'>

export function useImpostazioni(strutturaId: string | null) {
  return useQuery({
    queryKey: ['impostazioni', strutturaId],
    queryFn: () => apiGet<ImpostazioniStrutturaDto>(`/strutture/${strutturaId}/impostazioni`),
    enabled: !!strutturaId,
  })
}

export function useAggiornaImpostazioni(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: ImpostazioniStrutturaRequest) => apiPut<ImpostazioniStrutturaDto>(`/strutture/${strutturaId}/impostazioni`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['impostazioni', strutturaId] }),
  })
}
