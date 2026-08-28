import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'
import { isoLocale } from '../lib/date'

// Come StatoCamera in api/camere.ts: l'Api serializza gli enum come numeri, non come stringhe.
export const StatoPrenotazione = { InCorso: 1, Incompleta: 2, Annullata: 3, Completata: 4 } as const
export type StatoPrenotazione = (typeof StatoPrenotazione)[keyof typeof StatoPrenotazione]

export interface PrenotazioneDto {
  id: string
  strutturaId: string
  cameraId: string | null
  cameraNome: string | null
  agenzia: string | null
  numeroPrenotazione: string | null
  importoPrenotazione: number | null
  importoPagato: number | null
  importoTotale: number | null
  checkIn: string | null
  checkOut: string | null
  numeroOspiti: number | null
  statePolice: boolean
  pms: boolean
  payTourist: boolean
  anno: number
  totalTax: number | null
  statoPrenotazione: StatoPrenotazione | null
  /** I 3 toggle per prenotazione. Se tassaSoggiornoAttiva è false, statePolice/pms/payTourist sono stati marcati "già inviati" alla creazione, senza inviare nulla. */
  tassaSoggiornoAttiva: boolean
  spesePuliziaAttiva: boolean
  cauzioneAttiva: boolean
  /** Ospite capofamiglia, se la scheda alloggiati è già stata compilata (null altrimenti). */
  ospiteNome: string | null
  ospiteCognome: string | null
}

export interface PrenotazioneRequest {
  cameraId: string
  agenzia: string | null
  numeroPrenotazione: string | null
  importoPrenotazione: number | null
  importoPagato: number | null
  importoTotale: number | null
  checkIn: string
  checkOut: string
  numeroOspiti: number | null
  tassaSoggiornoAttiva: boolean
  spesePuliziaAttiva: boolean
  cauzioneAttiva: boolean
}

export interface PreventivoDto {
  notti: number
  totale: number
}

function chiaviPrenotazioni(strutturaId: string | null) {
  return ['prenotazioni', strutturaId] as const
}

export function useArriviInCorso(strutturaId: string | null) {
  return useQuery({
    queryKey: ['prenotazioni', strutturaId, 'in-corso'],
    queryFn: () => apiGet<PrenotazioneDto[]>(`/strutture/${strutturaId}/prenotazioni?vista=in-corso`),
    enabled: !!strutturaId,
  })
}

export function useArriviProssimi(strutturaId: string | null) {
  return useQuery({
    queryKey: ['prenotazioni', strutturaId, 'arrivi'],
    queryFn: () => apiGet<PrenotazioneDto[]>(`/strutture/${strutturaId}/prenotazioni?vista=arrivi`),
    enabled: !!strutturaId,
  })
}

export function useStoricoPrenotazioni(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['prenotazioni', strutturaId, 'storico', anno],
    queryFn: () => apiGet<PrenotazioneDto[]>(`/strutture/${strutturaId}/prenotazioni?vista=storico&anno=${anno}`),
    enabled: !!strutturaId,
  })
}

export function usePrenotazioniPeriodo(strutturaId: string | null, dataInizio: Date, dataFine: Date) {
  const iniIso = isoLocale(dataInizio)
  const fineIso = isoLocale(dataFine)
  return useQuery({
    queryKey: ['prenotazioni', strutturaId, 'periodo', iniIso, fineIso],
    queryFn: () =>
      apiGet<PrenotazioneDto[]>(
        `/strutture/${strutturaId}/prenotazioni?vista=periodo&dataInizio=${iniIso}&dataFine=${fineIso}`,
      ),
    enabled: !!strutturaId,
  })
}

export function usePreventivo(
  strutturaId: string | null,
  cameraId: string | null,
  checkIn: string | null,
  checkOut: string | null,
  numeroOspiti: number,
) {
  return useQuery({
    queryKey: ['preventivo', strutturaId, cameraId, checkIn, checkOut, numeroOspiti],
    queryFn: () =>
      apiGet<PreventivoDto>(
        `/strutture/${strutturaId}/prezzi-camera/preventivo?cameraId=${cameraId}&checkIn=${checkIn}&checkOut=${checkOut}&numeroOspiti=${numeroOspiti}`,
      ),
    enabled: !!strutturaId && !!cameraId && !!checkIn && !!checkOut && !!numeroOspiti,
    retry: false,
  })
}

function useInvalidaPrenotazioni(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: chiaviPrenotazioni(strutturaId) })
}

export function useCreaPrenotazione(strutturaId: string | null) {
  const invalida = useInvalidaPrenotazioni(strutturaId)
  return useMutation({
    mutationFn: (request: PrenotazioneRequest) => apiPost<PrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaPrenotazione(strutturaId: string | null) {
  const invalida = useInvalidaPrenotazioni(strutturaId)
  return useMutation({
    mutationFn: ({ prenotazioneId, request }: { prenotazioneId: string; request: PrenotazioneRequest }) =>
      apiPut<PrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}`, request),
    onSuccess: invalida,
  })
}

export function useAnnullaPrenotazione(strutturaId: string | null) {
  const invalida = useInvalidaPrenotazioni(strutturaId)
  return useMutation({
    mutationFn: (prenotazioneId: string) => apiPost<PrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/annulla`),
    onSuccess: invalida,
  })
}

export function useCheckIn(strutturaId: string | null) {
  const invalida = useInvalidaPrenotazioni(strutturaId)
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (prenotazioneId: string) => apiPost<PrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/check-in`),
    onSuccess: () => {
      invalida()
      queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] })
    },
  })
}

export function useCheckOut(strutturaId: string | null) {
  const invalida = useInvalidaPrenotazioni(strutturaId)
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ prenotazioneId, restituisciCauzione, importoCauzioneTrattenuta }: { prenotazioneId: string; restituisciCauzione: boolean; importoCauzioneTrattenuta: number | null }) =>
      apiPost<PrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/check-out`, {
        restituisciCauzione,
        importoCauzioneTrattenuta,
      }),
    onSuccess: () => {
      invalida()
      queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] })
    },
  })
}
