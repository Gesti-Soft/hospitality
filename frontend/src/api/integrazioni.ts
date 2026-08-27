import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut, apiScaricaFile } from './client'

export interface WubookIntegrazioneDto {
  strutturaId: string
  attivo: boolean
  gestisoftUsername: string | null
  licenzaConfigurata: boolean
  credenzialiPronte: boolean
  cacheAggiornataAtUtc: string | null
  ultimoErrore: string | null
}

export interface AlloggiatiWebIntegrazioneDto {
  strutturaId: string
  utente: string | null
  credenzialiConfigurate: boolean
  ultimoInvioAtUtc: string | null
  ultimeSchedineInviate: number | null
  ultimoErrore: string | null
}

export interface OsservatorioAppartamentoDto {
  id: string
  strutturaId: string
  nome: string | null
  entityCode: string | null
  hotelCode: string | null
  credenzialiConfigurate: boolean
  tipologieIds: string[]
  cursoreDataAtUtc: string | null
  ultimoInvioAtUtc: string | null
  ultimeSchedineInviate: number | null
  ultimoErrore: string | null
}

export interface PayTouristIntegrazioneDto {
  strutturaId: string
  tokenConfigurato: boolean
  portaleOnlineAttivo: boolean
}

export interface PayTouristStrutturaDto {
  id: string
  strutturaId: string
  nome: string | null
  idStrutturaPaytourist: number | null
  tipologieIds: string[]
  ultimoInvioAtUtc: string | null
  ultimeInviate: number | null
  ultimoErrore: string | null
}

export function useWubookConfig(strutturaId: string | null) {
  return useQuery({
    queryKey: ['wubook-config', strutturaId],
    queryFn: () => apiGet<WubookIntegrazioneDto>(`/strutture/${strutturaId}/wubook/config`),
    enabled: !!strutturaId,
  })
}

export function useAlloggiatiWebConfig(strutturaId: string | null) {
  return useQuery({
    queryKey: ['alloggiati-web-config', strutturaId],
    queryFn: () => apiGet<AlloggiatiWebIntegrazioneDto>(`/strutture/${strutturaId}/alloggiati-web/config`),
    enabled: !!strutturaId,
  })
}

export function useOsservatorioAppartamenti(strutturaId: string | null) {
  return useQuery({
    queryKey: ['osservatorio-appartamenti', strutturaId],
    queryFn: () => apiGet<OsservatorioAppartamentoDto[]>(`/strutture/${strutturaId}/osservatorio/appartamenti`),
    enabled: !!strutturaId,
  })
}

export function usePayTouristConfig(strutturaId: string | null) {
  return useQuery({
    queryKey: ['paytourist-config', strutturaId],
    queryFn: () => apiGet<PayTouristIntegrazioneDto>(`/strutture/${strutturaId}/paytourist/config`),
    enabled: !!strutturaId,
  })
}

export function usePayTouristStrutture(strutturaId: string | null) {
  return useQuery({
    queryKey: ['paytourist-strutture', strutturaId],
    queryFn: () => apiGet<PayTouristStrutturaDto[]>(`/strutture/${strutturaId}/paytourist/strutture`),
    enabled: !!strutturaId,
  })
}

// ---------------------------------------------------------------------------
// Alloggiati Web (Polizia di Stato)
// ---------------------------------------------------------------------------

export interface AlloggiatiWebConfigRequest {
  utente: string | null
  password: string | null
  wsKey: string | null
}

export interface RisultatoInvioAlloggiatiWebDto {
  inviate: number
  totaleSchedine: number
  errori: string[]
  messaggio: string | null
}

export function useAggiornaAlloggiatiWebConfig(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: AlloggiatiWebConfigRequest) => apiPut<AlloggiatiWebIntegrazioneDto>(`/strutture/${strutturaId}/alloggiati-web/config`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alloggiati-web-config', strutturaId] }),
  })
}

export function useInviaAlloggiatiWebOra(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<RisultatoInvioAlloggiatiWebDto>(`/strutture/${strutturaId}/alloggiati-web/schedine/invia`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alloggiati-web-config', strutturaId] }),
  })
}

export function esportaSchedineAlloggiatiWeb(strutturaId: string) {
  return apiScaricaFile(`/strutture/${strutturaId}/alloggiati-web/schedine/export`, `schedine-alloggiati-web.txt`)
}

// ---------------------------------------------------------------------------
// Osservatorio Turistico
// ---------------------------------------------------------------------------

export interface OsservatorioAppartamentoRequest {
  nome: string | null
  entityCode: string | null
  password: string | null
  hotelCode: string | null
  tipologieIds: string[]
}

export interface RisultatoInvioOsservatorioDto {
  arriviInviati: number
  checkoutInviati: number
  giorniChiusi: number
  messaggio: string | null
}

function useInvalidaOsservatorio(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['osservatorio-appartamenti', strutturaId] })
}

export function useCreaOsservatorioAppartamento(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: (request: OsservatorioAppartamentoRequest) => apiPost<OsservatorioAppartamentoDto>(`/strutture/${strutturaId}/osservatorio/appartamenti`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaOsservatorioAppartamento(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: ({ appartamentoId, request }: { appartamentoId: string; request: OsservatorioAppartamentoRequest }) =>
      apiPut<OsservatorioAppartamentoDto>(`/strutture/${strutturaId}/osservatorio/appartamenti/${appartamentoId}`, request),
    onSuccess: invalida,
  })
}

export function useInviaOsservatorioOra(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: (appartamentoId: string) =>
      apiPost<RisultatoInvioOsservatorioDto>(`/strutture/${strutturaId}/osservatorio/appartamenti/${appartamentoId}/invia`),
    onSuccess: invalida,
  })
}

// ---------------------------------------------------------------------------
// PayTourist
// ---------------------------------------------------------------------------

export interface PayTouristConfigRequest {
  token: string | null
  portaleOnlineAttivo: boolean
}

export interface PayTouristStrutturaRequest {
  nome: string | null
  idStrutturaPaytourist: number | null
  tipologieIds: string[]
}

export interface RisultatoInvioPayTouristDto {
  inviate: number
  totalePrenotazioni: number
  errori: string[]
  messaggio: string | null
}

export function useAggiornaPayTouristConfig(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: PayTouristConfigRequest) => apiPut<PayTouristIntegrazioneDto>(`/strutture/${strutturaId}/paytourist/config`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['paytourist-config', strutturaId] }),
  })
}

function useInvalidaPayTouristStrutture(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['paytourist-strutture', strutturaId] })
}

export function useCreaPayTouristStruttura(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: (request: PayTouristStrutturaRequest) => apiPost<PayTouristStrutturaDto>(`/strutture/${strutturaId}/paytourist/strutture`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaPayTouristStruttura(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: ({ payTouristStrutturaId, request }: { payTouristStrutturaId: string; request: PayTouristStrutturaRequest }) =>
      apiPut<PayTouristStrutturaDto>(`/strutture/${strutturaId}/paytourist/strutture/${payTouristStrutturaId}`, request),
    onSuccess: invalida,
  })
}

export function useInviaPayTouristOra(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: () => apiPost<RisultatoInvioPayTouristDto>(`/strutture/${strutturaId}/paytourist/invia`),
    onSuccess: invalida,
  })
}

export function esportaPayTourist(strutturaId: string, payTouristStrutturaId: string) {
  return apiScaricaFile(`/strutture/${strutturaId}/paytourist/strutture/${payTouristStrutturaId}/export`, `paytourist-export.json`)
}

// ---------------------------------------------------------------------------
// Wubook
// ---------------------------------------------------------------------------

export interface WubookConfigRequest {
  attivo: boolean
  gestisoftUsername: string | null
  gestisoftToken: string | null
}

export interface RisultatoSincronizzazioneWubookDto {
  importate: number
  aggiornate: number
  annullate: number
  errori: string[]
}

function useInvalidaWubook(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['wubook-config', strutturaId] })
}

export function useAggiornaWubookConfig(strutturaId: string | null) {
  const invalida = useInvalidaWubook(strutturaId)
  return useMutation({
    mutationFn: (request: WubookConfigRequest) => apiPut<WubookIntegrazioneDto>(`/strutture/${strutturaId}/wubook/config`, request),
    onSuccess: invalida,
  })
}

export function useRinnovaWubookCredenziali(strutturaId: string | null) {
  const invalida = useInvalidaWubook(strutturaId)
  return useMutation({
    mutationFn: () => apiPost<WubookIntegrazioneDto>(`/strutture/${strutturaId}/wubook/rinnova`),
    onSuccess: invalida,
  })
}

export function useSincronizzaWubookPrezzi(strutturaId: string | null) {
  return useMutation({
    mutationFn: ({ dataInizio, dataFine }: { dataInizio: string; dataFine: string }) =>
      apiPost<void>(`/strutture/${strutturaId}/wubook/prezzi/sincronizza`, { dataInizio, dataFine }),
  })
}

export function useSincronizzaWubookDisponibilita(strutturaId: string | null) {
  return useMutation({
    mutationFn: ({ dataInizio, dataFine }: { dataInizio: string; dataFine: string }) =>
      apiPost<void>(`/strutture/${strutturaId}/wubook/disponibilita/sincronizza`, { dataInizio, dataFine }),
  })
}

export function useSincronizzaWubookPrenotazioni(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<RisultatoSincronizzazioneWubookDto>(`/strutture/${strutturaId}/wubook/prenotazioni/sincronizza`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['prenotazioni', strutturaId] }),
  })
}

export function useSincronizzaWubookCamera(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (cameraId: string) => apiPost(`/strutture/${strutturaId}/wubook/camere/${cameraId}/sincronizza`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] }),
  })
}

export function useRimuoviWubookCamera(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (cameraId: string) => apiDelete(`/strutture/${strutturaId}/wubook/camere/${cameraId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['camere', strutturaId] }),
  })
}
