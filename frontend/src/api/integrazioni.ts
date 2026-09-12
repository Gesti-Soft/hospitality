import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut, apiScaricaFile } from './client'
import { confrontaNaturale } from '../lib/ordinamento'

export interface WubookIntegrazioneDto {
  strutturaId: string
  attivo: boolean
  credenzialiPronte: boolean
  ultimoErrore: string | null
}

export interface AlloggiatiWebIntegrazioneDto {
  strutturaId: string
  utente: string | null
  credenzialiConfigurate: boolean
  ultimoInvioAtUtc: string | null
  ultimeSchedineInviate: number | null
  ultimoErrore: string | null
  ultimaVerificaOkAtUtc: string | null
}

export interface SchedinaAlloggiatiWebDto {
  ospiteId: string
  prenotazioneId: string | null
  nomeOspite: string
  camera: string | null
  checkIn: string | null
  checkOut: string | null
  inviata: boolean
  /** Termine di legge per la trasmissione: 24 ore dall'arrivo, 6 se il soggiorno dura meno di 24 ore. */
  scadenzaInvioUtc: string | null
  /** Soggiorno sotto le 24 ore: termine ridotto a 6 ore. */
  soggiornoBreve: boolean
  /** Ancora trasmissibile: a false l'invio non va offerto, il portale lo rifiuterebbe. */
  inTermine: boolean
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
  /** Giornata fino a cui l'appartamento risulta chiuso: gli arrivi anteriori non sono più trasmissibili. */
  chiusoFinoA: string | null
  /** Arrivo ancora trasmissibile: a false l'invio non va offerto. */
  inTermine: boolean
  /** Appartamento in cui va dichiarata, dedotto dalla tipologia della camera. Null = tipologia non associata a nessun appartamento. */
  appartamentoId: string | null
  appartamentoNome: string | null
}

export interface PrenotazionePayTouristDto {
  ospiteId: string
  prenotazioneId: string | null
  nomeOspite: string
  camera: string | null
  checkIn: string | null
  checkOut: string | null
  inviata: boolean
  /** Ultimo giorno utile per la trasmissione: 7 giorni dal check-out. */
  scadenzaInvioUtc: string | null
  /** Ancora trasmissibile: a false l'invio non va offerto. */
  inTermine: boolean
  /** Struttura PayTourist in cui va dichiarata, dedotta dalla tipologia della camera. Null = tipologia non associata. */
  payTouristStrutturaId: string | null
  payTouristStrutturaNome: string | null
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
  ultimaVerificaOkAtUtc: string | null
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
  ultimaVerificaOkAtUtc: string | null
}

/** Struttura così come restituita da PayTourist (GET api/v1/structures) — non ancora associata a una struttura PayTourist locale. */
export interface PayTouristStrutturaRemotaDto {
  id: number
  nome: string
}

/** Esito di un test di connessione reale eseguito lato server subito dopo il salvataggio — comune alle tre integrazioni esterne (Alloggiati Web, Osservatorio, PayTourist). */
export interface EsitoVerificaConnessione {
  connessioneOk: boolean
  connessioneErrore: string | null
}

/** Una riduzione così come configurata su PayTourist (GET api/v1/reductions) — elenco grezzo, per la verifica manuale del suggerimento età. */
export interface PayTouristRiduzioneDto {
  id: number
  nome: string
  descrizione: string | null
  percentuale: string | null
}

/** Suggerimento best-effort per soglie età e percentuali di riduzione di Impostazioni → Tassa di soggiorno — mai da salvare senza che l'operatore lo confermi (vedi PayTouristConfigService.SuggerisciEtaEsenzioneTassaAsync). */
export interface SuggerimentoEtaTassaDto {
  etaMinori: number | null
  etaAnziani: number | null
  percentualeResidenti: number | null
  percentualeMinori: number | null
  percentualeAnziani: number | null
  riduzioni: PayTouristRiduzioneDto[]
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

/** Azione on-demand (non una query in cache): l'operatore la lancia cliccando un pulsante in Impostazioni, non va rieseguita ad ogni render. */
export function useSuggerimentoEtaTassaPayTourist(strutturaId: string | null) {
  return useMutation({
    mutationFn: () => apiGet<SuggerimentoEtaTassaDto>(`/strutture/${strutturaId}/paytourist/config/suggerimento-eta-tassa`),
  })
}

export function usePayTouristStrutture(strutturaId: string | null) {
  return useQuery({
    queryKey: ['paytourist-strutture', strutturaId],
    queryFn: () => apiGet<PayTouristStrutturaDto[]>(`/strutture/${strutturaId}/paytourist/strutture`),
    enabled: !!strutturaId,
  })
}

/** Strutture abilitate su PayTourist per il Token già configurato — per farle scegliere invece di digitare a mano lo structure_id. Disabilitato di default: va interrogato su richiesta esplicita dell'operatore (apertura dialog), non ad ogni render. */
export function usePayTouristStruttureDisponibili(strutturaId: string | null, abilitato: boolean) {
  return useQuery({
    queryKey: ['paytourist-strutture-disponibili', strutturaId],
    queryFn: () => apiGet<PayTouristStrutturaRemotaDto[]>(`/strutture/${strutturaId}/paytourist/strutture/disponibili`),
    enabled: !!strutturaId && abilitato,
    retry: false,
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
  /** Conteggio, non un elenco — i dettagli sono in messaggio. */
  errori: number
  messaggio: string | null
}

export interface AggiornaAlloggiatiWebConfigRisultatoDto extends EsitoVerificaConnessione {
  integrazione: AlloggiatiWebIntegrazioneDto
}

export function useAggiornaAlloggiatiWebConfig(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: AlloggiatiWebConfigRequest) =>
      apiPut<AggiornaAlloggiatiWebConfigRisultatoDto>(`/strutture/${strutturaId}/alloggiati-web/config`, request),
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

export function useInviaSchedinaAlloggiatiWebSingola(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ospiteId: string) =>
      apiPost<RisultatoInvioAlloggiatiWebDto>(`/strutture/${strutturaId}/alloggiati-web/schedine/${ospiteId}/invia`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alloggiati-web-schedine', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['alloggiati-web-config', strutturaId] })
    },
  })
}

export function esportaSchedineAlloggiatiWeb(strutturaId: string, anno: number) {
  return apiScaricaFile(`/strutture/${strutturaId}/alloggiati-web/schedine/export?anno=${anno}`, `schedine-alloggiati-web-${anno}.txt`)
}

export function esportaSchedinaAlloggiatiWebSingola(strutturaId: string, ospiteId: string, anno: number) {
  return apiScaricaFile(`/strutture/${strutturaId}/alloggiati-web/schedine/${ospiteId}/export?anno=${anno}`, `schedina-alloggiati-web.txt`)
}

/** Su richiesta esplicita, filtrato per anno selezionato (non più una finestra mobile di 30 giorni). */
export function useSchedineAlloggiatiWeb(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['alloggiati-web-schedine', strutturaId, anno],
    queryFn: () => apiGet<SchedinaAlloggiatiWebDto[]>(`/strutture/${strutturaId}/alloggiati-web/schedine?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

/** Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti. */
export function useAnniAlloggiatiWeb(strutturaId: string | null) {
  return useQuery({
    queryKey: ['alloggiati-web-anni', strutturaId],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/alloggiati-web/schedine/anni`),
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

export interface SalvaOsservatorioAppartamentoRisultatoDto extends EsitoVerificaConnessione {
  appartamento: OsservatorioAppartamentoDto
}

export function useCreaOsservatorioAppartamento(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: (request: OsservatorioAppartamentoRequest) =>
      apiPost<SalvaOsservatorioAppartamentoRisultatoDto>(`/strutture/${strutturaId}/osservatorio/appartamenti`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaOsservatorioAppartamento(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: ({ appartamentoId, request }: { appartamentoId: string; request: OsservatorioAppartamentoRequest }) =>
      apiPut<SalvaOsservatorioAppartamentoRisultatoDto>(`/strutture/${strutturaId}/osservatorio/appartamenti/${appartamentoId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaOsservatorioAppartamento(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    mutationFn: (appartamentoId: string) => apiDelete<void>(`/strutture/${strutturaId}/osservatorio/appartamenti/${appartamentoId}`),
    onSuccess: invalida,
  })
}

export function useInviaOsservatorioOra(strutturaId: string | null) {
  const invalida = useInvalidaOsservatorio(strutturaId)
  return useMutation({
    // Tutti gli appartamenti in un colpo: la schermata non ne fa più scegliere uno.
    mutationFn: () => apiPost<RisultatoInvioOsservatorioDto>(`/strutture/${strutturaId}/osservatorio/invia`),
    onSuccess: invalida,
  })
}

/** Su richiesta esplicita, filtrato per anno selezionato (non più una finestra mobile di 30 giorni). */
export function useInviaArrivoOsservatorioSingolo(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ospiteId: string) =>
      apiPost<RisultatoInvioOsservatorioDto>(`/strutture/${strutturaId}/osservatorio/schedine/${ospiteId}/invia`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['osservatorio-schedine', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['osservatorio-appartamenti', strutturaId] })
    },
  })
}

/** Elenco di tutta la Struttura: ogni riga porta con sé l'appartamento in cui va dichiarata, dedotto dalla tipologia della camera. */
export function useSchedineOsservatorio(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['osservatorio-schedine', strutturaId, anno],
    queryFn: () => apiGet<SchedinaOsservatorioDto[]>(`/strutture/${strutturaId}/osservatorio/schedine?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

/** Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti. A livello di Struttura, non di singolo appartamento. */
export function useAnniOsservatorio(strutturaId: string | null) {
  return useQuery({
    queryKey: ['osservatorio-anni', strutturaId],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/osservatorio/anni`),
    enabled: !!strutturaId,
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
  /** Conteggio, non un elenco — i dettagli sono in messaggio. */
  errori: number
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

export interface SalvaPayTouristStrutturaRisultatoDto extends EsitoVerificaConnessione {
  struttura: PayTouristStrutturaDto
}

export function useCreaPayTouristStruttura(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: (request: PayTouristStrutturaRequest) =>
      apiPost<SalvaPayTouristStrutturaRisultatoDto>(`/strutture/${strutturaId}/paytourist/strutture`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaPayTouristStruttura(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: ({ payTouristStrutturaId, request }: { payTouristStrutturaId: string; request: PayTouristStrutturaRequest }) =>
      apiPut<SalvaPayTouristStrutturaRisultatoDto>(`/strutture/${strutturaId}/paytourist/strutture/${payTouristStrutturaId}`, request),
    onSuccess: invalida,
  })
}

export function useEliminaPayTouristStruttura(strutturaId: string | null) {
  const invalida = useInvalidaPayTouristStrutture(strutturaId)
  return useMutation({
    mutationFn: (payTouristStrutturaId: string) => apiDelete<void>(`/strutture/${strutturaId}/paytourist/strutture/${payTouristStrutturaId}`),
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

export function useInviaPayTouristSingola(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ospiteId: string) =>
      apiPost<void>(`/strutture/${strutturaId}/paytourist/prenotazioni/${ospiteId}/invia`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['paytourist-prenotazioni', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['paytourist-strutture', strutturaId] })
    },
  })
}

/** Esporta in un solo file tutte le strutture PayTourist configurate: la schermata non ne fa più scegliere una. */
export function esportaPayTourist(strutturaId: string) {
  return apiScaricaFile(`/strutture/${strutturaId}/paytourist/export`, `paytourist-${new Date().toISOString().slice(0, 10)}.json`)
}


/** Su richiesta esplicita, filtrato per anno selezionato (non più una finestra mobile di 30 giorni). */
/** Elenco di tutta la Struttura: ogni riga porta con sé la struttura PayTourist in cui va dichiarata, dedotta dalla tipologia della camera. */
export function usePrenotazioniPayTourist(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['paytourist-prenotazioni', strutturaId, anno],
    queryFn: () => apiGet<PrenotazionePayTouristDto[]>(`/strutture/${strutturaId}/paytourist/prenotazioni?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

/** Anni con almeno una prenotazione — per non proporre nel selettore Anno anni sicuramente vuoti. */
export function useAnniPayTourist(strutturaId: string | null) {
  return useQuery({
    queryKey: ['paytourist-anni', strutturaId],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/paytourist/anni`),
    enabled: !!strutturaId,
  })
}

// ---------------------------------------------------------------------------
// Wubook
// ---------------------------------------------------------------------------

export interface WubookConfigRequest {
  attivo: boolean
}

export interface RisultatoSincronizzazioneWubookDto {
  importate: number
  aggiornate: number
  annullate: number
  /** Conteggio, non un elenco. */
  errori: number
}

/** Una prenotazione Wubook intercettata (Lcode/Rcode), a prescindere dall'esito dell'importazione — per recuperarla a mano in caso di problemi. */
export interface WubookEventoRicevutoDto {
  id: string
  lcode: string
  rcode: number
  importazioneRiuscita: boolean
  messaggioErrore: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export function useWubookEventiRicevuti(strutturaId: string | null, abilitato: boolean) {
  return useQuery({
    queryKey: ['wubook-eventi-ricevuti', strutturaId],
    queryFn: () => apiGet<WubookEventoRicevutoDto[]>(`/strutture/${strutturaId}/wubook/eventi-ricevuti`),
    enabled: !!strutturaId && abilitato,
  })
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

export function useSincronizzaWubookTipologia(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (tipologiaId: string) => apiPost(`/strutture/${strutturaId}/wubook/tipologie/${tipologiaId}/sincronizza`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook-remote', strutturaId] })
    },
  })
}

export function useRimuoviWubookTipologia(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (tipologiaId: string) => apiDelete(`/strutture/${strutturaId}/wubook/tipologie/${tipologiaId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook', strutturaId] })
    },
  })
}

/** Elimina da OTA un pool senza (o senza più) una Tipologia locale associata — a differenza di useRimuoviWubookTipologia, che opera su una tipologia locale. */
export function useRimuoviWubookTipologiaRemota(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (idCameraWubook: number) => apiDelete(`/strutture/${strutturaId}/wubook/tipologie/remote/${idCameraWubook}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook-remote', strutturaId] })
    },
  })
}

export interface TipologiaWubookInfoDto {
  tipologiaId: string
  tipologiaNome: string
  camereCollegate: number
  chiusureCount: number
  restrizioniCount: number
  idCameraWubook: number | null
  wubookAttiva: boolean
}

export interface CameraWubookRemoteDto {
  id: number
  nome: string
  shortName: string | null
  occupancy: number
  prezzo: number
  disponibilita: number
}

/** Tipologie della struttura con conteggio camere reali collegate e stato associazione OTA — per la tab Camere della pagina Servizi OTA. */
export function useTipologiePerAssociazione(strutturaId: string | null) {
  return useQuery({
    queryKey: ['tipologie-wubook', strutturaId],
    queryFn: () => apiGet<TipologiaWubookInfoDto[]>(`/strutture/${strutturaId}/wubook/tipologie/per-associazione`),
    enabled: !!strutturaId,
    select: (tipologie) => [...tipologie].sort((a, b) => confrontaNaturale(a.tipologiaNome, b.tipologiaNome)),
  })
}

/** Camere già presenti su Wubook (fetch_rooms) — caricate solo quando serve, è una vera chiamata Wubook. */
export function useCamereRemoteWubook(strutturaId: string | null, abilitato: boolean) {
  return useQuery({
    queryKey: ['tipologie-wubook-remote', strutturaId],
    queryFn: () => apiGet<CameraWubookRemoteDto[]>(`/strutture/${strutturaId}/wubook/tipologie/remote`),
    enabled: !!strutturaId && abilitato,
  })
}

export function useAssociaTipologiaWubook(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ tipologiaId, idCameraWubook }: { tipologiaId: string; idCameraWubook: number | null }) =>
      apiPut(`/strutture/${strutturaId}/wubook/tipologie/${tipologiaId}/associazione`, { idCameraWubook }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tipologie-wubook', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['tipologie-camera', strutturaId] })
    },
  })
}

// ---------------------------------------------------------------------------
// Chiusure camera per periodo
// ---------------------------------------------------------------------------

export interface ChiusuraCameraDto {
  /** null per le righe derivate al volo da una prenotazione reale (v. `origine`) — mai eliminabili. */
  id: string | null
  cameraId: string
  dataInizio: string
  dataFine: string
  motivo: string | null
  quantita: number | null
  origine: 'Manuale' | 'Prenotazione'
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
