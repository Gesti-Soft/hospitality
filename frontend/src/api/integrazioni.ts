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

export interface SchedinaAlloggiatiWebDto {
  ospiteId: string
  prenotazioneId: string | null
  nomeOspite: string
  camera: string | null
  checkIn: string | null
  checkOut: string | null
  inviata: boolean
}

export interface SchedinaOsservatorioDto {
  ospiteId: string
  prenotazioneId: string | null
  nomeOspite: string
  camera: string | null
  checkIn: string | null
  checkOut: string | null
  arrivoInviato: boolean
  partenzaInviata: boolean | null
}

export interface PrenotazionePayTouristDto {
  ospiteId: string
  prenotazioneId: string | null
  nomeOspite: string
  camera: string | null
  checkIn: string | null
  checkOut: string | null
  inviata: boolean
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

export function useSchedineAlloggiatiWeb(strutturaId: string | null) {
  return useQuery({
    queryKey: ['alloggiati-web-schedine', strutturaId],
    queryFn: () => apiGet<SchedinaAlloggiatiWebDto[]>(`/strutture/${strutturaId}/alloggiati-web/schedine`),
    enabled: !!strutturaId,
  })
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

export function useSchedineOsservatorio(strutturaId: string | null, appartamentoId: string | null) {
  return useQuery({
    queryKey: ['osservatorio-schedine', strutturaId, appartamentoId],
    queryFn: () => apiGet<SchedinaOsservatorioDto[]>(`/strutture/${strutturaId}/osservatorio/appartamenti/${appartamentoId}/schedine`),
    enabled: !!strutturaId && !!appartamentoId,
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

export function usePrenotazioniPayTourist(strutturaId: string | null, payTouristStrutturaId: string | null) {
  return useQuery({
    queryKey: ['paytourist-prenotazioni', strutturaId, payTouristStrutturaId],
    queryFn: () => apiGet<PrenotazionePayTouristDto[]>(`/strutture/${strutturaId}/paytourist/strutture/${payTouristStrutturaId}/prenotazioni`),
    enabled: !!strutturaId && !!payTouristStrutturaId,
  })
}

// ---------------------------------------------------------------------------
// Wubook
// ---------------------------------------------------------------------------

export interface WubookConfigRequest {
  attivo: boolean
}

export interface WubookLicenzaRequest {
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

// Separato da useAggiornaWubookConfig apposta: due submit indipendenti (toggle Attivo vs licenza gestisoft.it).
export function useAggiornaWubookLicenza(strutturaId: string | null) {
  const invalida = useInvalidaWubook(strutturaId)
  return useMutation({
    mutationFn: (request: WubookLicenzaRequest) => apiPut<WubookIntegrazioneDto>(`/strutture/${strutturaId}/wubook/licenza`, request),
    onSuccess: invalida,
  })
}

export function useRinnovaWubookCredenziali(strutturaId: string | null) {
  const invalida = useInvalidaWubook(strutturaId)
  return useMutation({
    mutationFn: () => apiPost<WubookIntegrazioneDto>(`/strutture/${strutturaId}/wubook/config/rinnova`),
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

// ---------------------------------------------------------------------------
// Chiusure camera per periodo
// ---------------------------------------------------------------------------

export interface ChiusuraCameraDto {
  id: string
  cameraId: string
  dataInizio: string
  dataFine: string
  motivo: string | null
  quantita: number | null
}

export interface CreaChiusuraCameraRequest {
  dataInizio: string
  dataFine: string
  motivo: string | null
  quantita: number | null
}

export function useChiusureCamera(strutturaId: string | null, cameraId: string | null) {
  return useQuery({
    queryKey: ['wubook-chiusure', strutturaId, cameraId],
    queryFn: () => apiGet<ChiusuraCameraDto[]>(`/strutture/${strutturaId}/wubook/camere/${cameraId}/chiusure`),
    enabled: !!strutturaId && !!cameraId,
  })
}

export function useCreaChiusuraCamera(strutturaId: string | null, cameraId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreaChiusuraCameraRequest) => apiPost<ChiusuraCameraDto>(`/strutture/${strutturaId}/wubook/camere/${cameraId}/chiusure`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wubook-chiusure', strutturaId, cameraId] }),
  })
}

export function useEliminaChiusuraCamera(strutturaId: string | null, cameraId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (chiusuraId: string) => apiDelete(`/strutture/${strutturaId}/wubook/camere/${cameraId}/chiusure/${chiusuraId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wubook-chiusure', strutturaId, cameraId] }),
  })
}

// ---------------------------------------------------------------------------
// Restrizioni soggiorno camera per periodo
// ---------------------------------------------------------------------------

export interface RestrizioneSoggiornoCameraDto {
  id: string
  cameraId: string
  dataInizio: string
  dataFine: string
  minStay: number | null
  maxStay: number | null
  motivo: string | null
}

export interface CreaRestrizioneSoggiornoCameraRequest {
  dataInizio: string
  dataFine: string
  minStay: number | null
  maxStay: number | null
  motivo: string | null
}

export function useRestrizioniPeriodoCamera(strutturaId: string | null, cameraId: string | null) {
  return useQuery({
    queryKey: ['wubook-restrizioni-periodo', strutturaId, cameraId],
    queryFn: () => apiGet<RestrizioneSoggiornoCameraDto[]>(`/strutture/${strutturaId}/wubook/camere/${cameraId}/restrizioni-periodo`),
    enabled: !!strutturaId && !!cameraId,
  })
}

export function useCreaRestrizionePeriodoCamera(strutturaId: string | null, cameraId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreaRestrizioneSoggiornoCameraRequest) =>
      apiPost<RestrizioneSoggiornoCameraDto>(`/strutture/${strutturaId}/wubook/camere/${cameraId}/restrizioni-periodo`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wubook-restrizioni-periodo', strutturaId, cameraId] }),
  })
}

export function useEliminaRestrizionePeriodoCamera(strutturaId: string | null, cameraId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (restrizioneId: string) => apiDelete(`/strutture/${strutturaId}/wubook/camere/${cameraId}/restrizioni-periodo/${restrizioneId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['wubook-restrizioni-periodo', strutturaId, cameraId] }),
  })
}

// ---------------------------------------------------------------------------
// Piani prezzo nominati/virtuali
// ---------------------------------------------------------------------------

export interface PianoPrezzoDto {
  id: number
  nome: string
  daily: boolean
  isVirtual: boolean
  parentId: number | null
  variazione: number | null
  tipoVariazione: number | null
}

export interface CreaPianoPrezzoRequest {
  nome: string
  parentId: number
  tipoVariazione: number
  variazione: number
}

export interface AggiornaPianoPrezzoRequest {
  nome: string | null
  tipoVariazione: number | null
  variazione: number | null
}

export function usePianiPrezzo(strutturaId: string | null) {
  return useQuery({
    queryKey: ['wubook-piani-prezzo', strutturaId],
    queryFn: () => apiGet<PianoPrezzoDto[]>(`/strutture/${strutturaId}/wubook/piani-prezzo`),
    enabled: !!strutturaId,
  })
}

function useInvalidaPianiPrezzo(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['wubook-piani-prezzo', strutturaId] })
}

export function useCreaPianoPrezzo(strutturaId: string | null) {
  const invalida = useInvalidaPianiPrezzo(strutturaId)
  return useMutation({
    mutationFn: (request: CreaPianoPrezzoRequest) => apiPost<{ id: number }>(`/strutture/${strutturaId}/wubook/piani-prezzo`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaPianoPrezzo(strutturaId: string | null) {
  const invalida = useInvalidaPianiPrezzo(strutturaId)
  return useMutation({
    mutationFn: ({ pianoId, request }: { pianoId: number; request: AggiornaPianoPrezzoRequest }) =>
      apiPut<void>(`/strutture/${strutturaId}/wubook/piani-prezzo/${pianoId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaPianoPrezzo(strutturaId: string | null) {
  const invalida = useInvalidaPianiPrezzo(strutturaId)
  return useMutation({
    mutationFn: (pianoId: number) => apiDelete(`/strutture/${strutturaId}/wubook/piani-prezzo/${pianoId}`),
    onSuccess: invalida,
  })
}

// ---------------------------------------------------------------------------
// Piani restrizione nominati
// ---------------------------------------------------------------------------

export interface RegoleRestrizioneDto {
  minStay: number | null
  minStayArrival: number | null
  maxStay: number | null
  maxStayArrival: number | null
  chiuso: boolean | null
  chiusoArrivo: boolean | null
  chiusoPartenza: boolean | null
}

export interface PianoRestrizioneDto {
  id: number
  nome: string
  regole: RegoleRestrizioneDto | null
}

export interface CreaPianoRestrizioneRequest {
  nome: string
  regole: RegoleRestrizioneDto | null
}

export interface AggiornaPianoRestrizioneRequest {
  nome: string | null
  regole: RegoleRestrizioneDto | null
}

export function usePianiRestrizione(strutturaId: string | null) {
  return useQuery({
    queryKey: ['wubook-piani-restrizione', strutturaId],
    queryFn: () => apiGet<PianoRestrizioneDto[]>(`/strutture/${strutturaId}/wubook/piani-restrizione`),
    enabled: !!strutturaId,
  })
}

function useInvalidaPianiRestrizione(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['wubook-piani-restrizione', strutturaId] })
}

export function useCreaPianoRestrizione(strutturaId: string | null) {
  const invalida = useInvalidaPianiRestrizione(strutturaId)
  return useMutation({
    mutationFn: (request: CreaPianoRestrizioneRequest) => apiPost<{ id: number }>(`/strutture/${strutturaId}/wubook/piani-restrizione`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaPianoRestrizione(strutturaId: string | null) {
  const invalida = useInvalidaPianiRestrizione(strutturaId)
  return useMutation({
    mutationFn: ({ pianoId, request }: { pianoId: number; request: AggiornaPianoRestrizioneRequest }) =>
      apiPut<void>(`/strutture/${strutturaId}/wubook/piani-restrizione/${pianoId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaPianoRestrizione(strutturaId: string | null) {
  const invalida = useInvalidaPianiRestrizione(strutturaId)
  return useMutation({
    mutationFn: (pianoId: number) => apiDelete(`/strutture/${strutturaId}/wubook/piani-restrizione/${pianoId}`),
    onSuccess: invalida,
  })
}
