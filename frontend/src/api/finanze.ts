import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface RiepilogoCassaDto {
  anno: number
  importoPagatoPrenotazioni: number
  cauzioni: number
  entrate: number
  spese: number
  saldo: number
  cassaAttuale: number
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

/** Anni con almeno un dato di cassa (spesa, entrata, cauzione o incasso prenotazione) — selettore Anno condiviso da Riepilogo/Spese/Entrate/Cauzioni. */
export function useAnniDisponibiliFinanze(strutturaId: string | null) {
  return useQuery({
    queryKey: ['finanze', strutturaId, 'anni'],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/finanze/anni`),
    enabled: !!strutturaId,
  })
}
