import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface RiepilogoCassaDto {
  anno: number
  importoPagatoPrenotazioni: number
  cauzioni: number
  entrate: number
  spese: number
  saldo: number
}

export function useRiepilogoCassa(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['finanze', strutturaId, 'riepilogo-cassa', anno],
    queryFn: () => apiGet<RiepilogoCassaDto>(`/strutture/${strutturaId}/finanze/riepilogo-cassa?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

export interface CauzioneDto {
  id: string
  strutturaId: string
  prenotazioneId: string
  importoCauzione: number | null
  dataInserimento: string | null
}

export function useCauzioni(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['finanze', strutturaId, 'cauzioni', anno],
    queryFn: () => apiGet<CauzioneDto[]>(`/strutture/${strutturaId}/finanze/cauzioni?anno=${anno}`),
    enabled: !!strutturaId,
  })
}
