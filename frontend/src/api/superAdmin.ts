import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut, apiScaricaFile } from './client'

export interface StrutturaAdminDto {
  id: string
  nome: string
  /** False = struttura "eliminata" (soft-delete) dal Cliente stesso o dal Super Admin. */
  attivo: boolean
  /** Valorizzato solo se !attivo — quando è passato da oltre 90 giorni la struttura è eliminabile definitivamente. */
  disattivataAtUtc: string | null
  wubookAttivo: boolean
  wubookUltimoErrore: string | null
  /** Licenza software GestiSoft di questa Struttura (NON la licenza Wubook) — scaduta = struttura inaccessibile ai suoi utenti. */
  scadenzaLicenza: string | null
  poliziaStatoAttiva: boolean
  osservatorioAttivo: boolean
  payTouristAttivo: boolean
  /** Concessi dal Super Admin per QUESTA struttura — distinti dai flag sopra, che sono il toggle self-service del Cliente. */
  wubookAbilitato: boolean
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
  /** Invio fatture allo SdI: concesso dal Super Admin / acceso dalla struttura. */
  sdiAbilitato: boolean
  sdiAttivo: boolean
  /** Struttura dimostrativa (dati inventati): si elimina definitivamente quando si vuole, senza i 90 giorni. */
  demo: boolean
  /** Solo per le demo: quando il Worker la elimina da sola (30 giorni dopo la creazione). */
  demoEliminazioneAtUtc: string | null
}

export interface ClienteAdminDto {
  id: string
  ragioneSociale: string
  partitaIva: string | null
  attivo: boolean
  createdAtUtc: string
  /** Quota annua pattuita con il Cliente — solo un promemoria, non genera fatture. */
  quotaAnnua: number | null
  /** Appunti liberi del Super Admin — mai visibile al Cliente stesso. */
  note: string | null
  numeroUtenti: number
  numeroUtentiAttivi: number
  strutture: StrutturaAdminDto[]
}

export interface UtenteAdminDto {
  id: string
  email: string
  nome: string | null
  cognome: string | null
  isSuperAdmin: boolean
  attivo: boolean
  clienteId: string | null
  clienteRagioneSociale: string | null
  createdAtUtc: string
  /** Titolare del Cliente: accesso libero a tutte le sue Strutture, senza bisogno di assegnazioni UtenteStruttura. */
  isClienteAccount: boolean
}

export interface DashboardSuperAdminDto {
  clienti: ClienteAdminDto[]
  utenti: UtenteAdminDto[]
}

export interface ServiziStrutturaRequest {
  wubookAbilitato: boolean
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
  sdiAbilitato: boolean
}

export function useDashboardSuperAdmin(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'dashboard'],
    queryFn: () => apiGet<DashboardSuperAdminDto>('/super-admin/dashboard'),
    enabled: abilitato,
  })
}

/** Versione installata del gestionale ("2.1", da deploy/update.sh); null fuori da un deploy. */
export function useVersioneGestionale(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'versione'],
    queryFn: () => apiGet<{ versione: string | null }>('/super-admin/versione'),
    enabled: abilitato,
    // Cambia solo con un deploy, che ricarica comunque la pagina.
    staleTime: Infinity,
  })
}

function useInvalidaDashboard() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
}

export function useImpostaAttivoCliente() {
  const invalida = useInvalidaDashboard()
  return useMutation({
    mutationFn: ({ clienteId, attivo }: { clienteId: string; attivo: boolean }) =>
      apiPut(`/super-admin/clienti/${clienteId}/attivo`, { attivo }),
    onSuccess: invalida,
  })
}

export function useAggiornaServiziStruttura() {
  const invalida = useInvalidaDashboard()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ strutturaId, request }: { strutturaId: string; request: ServiziStrutturaRequest }) =>
      apiPut(`/super-admin/strutture/${strutturaId}/servizi`, request),
    onSuccess: () => {
      invalida()
      // La sidebar (menu Polizia di Stato/Osservatorio/PayTourist/Wubook) legge i 4 flag dalle
      // Strutture già caricate in StrutturaContext — vanno rinfrescate subito, non solo la
      // dashboard, altrimenti una revoca appena fatta non si riflette finché non si ricarica.
      queryClient.invalidateQueries({ queryKey: ['strutture'] })
    },
  })
}

export function useResettaPasswordUtente() {
  return useMutation({
    mutationFn: ({ utenteId, nuovaPassword }: { utenteId: string; nuovaPassword: string }) =>
      apiPut(`/super-admin/utenti/${utenteId}/reset-password`, { nuovaPassword }),
  })
}

/** Toglie il blocco dopo 5 tentativi falliti, senza cambiare la password a chi se la ricorda. */
export function useSbloccaAccessoUtente() {
  return useMutation({
    mutationFn: ({ utenteId }: { utenteId: string }) => apiPut(`/super-admin/utenti/${utenteId}/sblocca`, {}),
  })
}

/**
 * Spegne il 2FA di un utente rimasto fuori (telefono e codici di recupero persi): è l'unico modo
 * per rimetterlo dentro, dato che il segreto vive solo sul suo telefono. Da lì riconfigura da capo.
 */
export function useResettaDueFattoriUtente() {
  return useMutation({
    mutationFn: ({ utenteId }: { utenteId: string }) => apiPut(`/super-admin/utenti/${utenteId}/reset-2fa`, {}),
  })
}

export interface AggiornaUtenteRequest {
  email: string
  nome: string | null
  cognome: string | null
  isClienteAccount: boolean
}

export function useAggiornaUtente() {
  const invalida = useInvalidaDashboard()
  return useMutation({
    mutationFn: ({ utenteId, request }: { utenteId: string; request: AggiornaUtenteRequest }) =>
      apiPut(`/super-admin/utenti/${utenteId}`, request),
    onSuccess: invalida,
  })
}

export function useImpostaAttivoUtente() {
  const invalida = useInvalidaDashboard()
  return useMutation({
    mutationFn: ({ utenteId, attivo }: { utenteId: string; attivo: boolean }) =>
      apiPut(`/super-admin/utenti/${utenteId}/attivo`, { attivo }),
    onSuccess: invalida,
  })
}

/** Eliminazione DEFINITIVA (hard delete, irreversibile) di una struttura disattivata da oltre 90 giorni. */
export function useEliminaStrutturaDefinitivamente() {
  const invalida = useInvalidaDashboard()
  return useMutation({
    mutationFn: (strutturaId: string) => apiDelete(`/super-admin/strutture/${strutturaId}`),
    onSuccess: invalida,
  })
}

/**
 * Crea la struttura dimostrativa "Hotel Belvedere" (20 camere, dati inventati) per il Cliente indicato,
 * o per un Cliente demo nuovo (nome ed email del titolare, password fissa "Demo." + anno + "!") se clienteId è null.
 */
export function useCreaStrutturaDemo() {
  const invalida = useInvalidaDashboard()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: { clienteId: string | null; nomeCliente: string | null; email: string | null }) =>
      apiPost<{ id: string; clienteId: string; nome: string; passwordDemo: string | null }>('/super-admin/strutture-demo', request),
    onSuccess: () => {
      invalida()
      queryClient.invalidateQueries({ queryKey: ['strutture'] })
    },
  })
}

/** Dump completo del database generato al volo — indipendente dai backup automatici notturni (vedi docs/backup-restore.md). */
export function scaricaBackupDatabase() {
  const timestamp = new Date().toISOString().slice(0, 19).replace(/[-:]/g, '').replace('T', '-')
  return apiScaricaFile('/super-admin/backup/export', `gestisoft-backup-${timestamp}.dump`)
}

export const GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA = 90

export function giorniDaDisattivazione(disattivataAtUtc: string): number {
  const ms = Date.now() - new Date(disattivataAtUtc).getTime()
  return Math.floor(ms / (1000 * 60 * 60 * 24))
}

// ---------------------------------------------------------------------------
// Canale SdI
// ---------------------------------------------------------------------------

/** Cosa ne è stato di un file ricevuto dallo SdI. Valori da GestiSoft.Domain.Enums.EsitoFileRicevutoSdi. */
export const EsitoFileRicevutoSdi = { Importata: 1, Duplicata: 2, SenzaDestinatario: 3, PiuDestinatari: 4, NonLeggibile: 5, Prova: 6 } as const
export type EsitoFileRicevutoSdi = (typeof EsitoFileRicevutoSdi)[keyof typeof EsitoFileRicevutoSdi]

export const ETICHETTA_ESITO_FILE_RICEVUTO: Record<EsitoFileRicevutoSdi, string> = {
  [EsitoFileRicevutoSdi.Importata]: 'Importata',
  [EsitoFileRicevutoSdi.Duplicata]: 'Già registrata',
  [EsitoFileRicevutoSdi.SenzaDestinatario]: 'Nessuna struttura',
  [EsitoFileRicevutoSdi.PiuDestinatari]: 'Più Clienti',
  [EsitoFileRicevutoSdi.NonLeggibile]: 'Non leggibile',
  [EsitoFileRicevutoSdi.Prova]: 'Collaudo',
}

/** I messaggi dello SdI su una fattura spedita. Valori da GestiSoft.Domain.Enums.TipoRicevutaSdi. */
export const ETICHETTA_TIPO_RICEVUTA_SDI: Record<number, string> = {
  1: 'Ricevuta di consegna',
  2: 'Mancata consegna',
  3: 'Scarto',
  4: 'Esito committente',
  5: 'Decorrenza termini',
  6: 'Attestazione di trasmissione',
  99: 'Non riconosciuta',
}

export interface FileRicevutoSdiDto {
  id: string
  identificativoSdi: string
  nomeFile: string
  fornitore: string | null
  /** Partita IVA o codice fiscale del destinatario scritti nella fattura. */
  identificativoCessionario: string | null
  esito: EsitoFileRicevutoSdi
  messaggio: string | null
  ricevutoAtUtc: string
}

export interface RicevutaSdiDto {
  id: string
  tipo: number
  identificativoSdi: string | null
  nomeFileFattura: string | null
  prova: boolean
  descrizione: string | null
  ricevutaAtUtc: string
}

export interface CanaleSdiDto {
  /** Assegnato dallo SdI al canale in ricezione; null finché non c'è. */
  codiceDestinatario: string | null
  codiceFiscaleTrasmittente: string | null
  /** Si spedisce allo SdI di collaudo: le fatture inviate non contano. */
  collaudo: boolean
  /** Ambiente di test: gli esiti del collaudo cambiano lo stato delle fatture, come in produzione. */
  collaudoApplicaEsiti: boolean
  quarantena: FileRicevutoSdiDto[]
  ricevuteNonAbbinate: RicevutaSdiDto[]
  ricezioniDiCollaudo: FileRicevutoSdiDto[]
}

export function useCanaleSdi(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'canale-sdi'],
    queryFn: () => apiGet<CanaleSdiDto>('/super-admin/canale-sdi'),
    enabled: abilitato,
  })
}

/** Dà a una struttura una fattura in quarantena: la vedranno tutte le strutture del suo Cliente. */
export function useAttribuisciFileRicevuto() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ fileRicevutoId, strutturaId }: { fileRicevutoId: string; strutturaId: string }) =>
      apiPost<void>(`/super-admin/canale-sdi/file-ricevuti/${fileRicevutoId}/attribuisci`, { strutturaId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['super-admin', 'canale-sdi'] }),
  })
}

export function scaricaFileRicevutoSdi(f: Pick<FileRicevutoSdiDto, 'id' | 'nomeFile'>) {
  return apiScaricaFile(`/super-admin/canale-sdi/file-ricevuti/${f.id}/file`, f.nomeFile, true)
}

export function scaricaRicevutaSdi(r: Pick<RicevutaSdiDto, 'id' | 'identificativoSdi'>) {
  return apiScaricaFile(`/super-admin/canale-sdi/ricevute/${r.id}/file`, `ricevuta-${r.identificativoSdi ?? r.id}.xml`, true)
}

// ---------------------------------------------------------------------------
// Archivio documenti fiscali (strutture eliminate)
// ---------------------------------------------------------------------------

/** Valori da GestiSoft.Domain.Enums.TipoDocumentoArchiviato. */
export const ETICHETTA_TIPO_DOCUMENTO_ARCHIVIATO: Record<number, string> = {
  1: 'Fattura emessa',
  2: 'Nota di credito',
  3: 'Ricevuta locazione breve',
  4: 'Fattura ricevuta',
}

export interface DocumentoArchiviatoDto {
  id: string
  clienteId: string
  clienteRagioneSociale: string
  strutturaNome: string
  strutturaPartitaIva: string | null
  tipo: number
  numero: string
  /** "YYYY-MM-DD" */
  data: string
  controparte: string | null
  importo: number | null
  haPdf: boolean
  haFile: boolean
  archiviatoAtUtc: string
  /** "YYYY-MM-DD": dal giorno dopo si cancella da solo. */
  scadenza: string
}

export function useArchivioDocumenti(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'archivio-documenti'],
    queryFn: () => apiGet<DocumentoArchiviatoDto[]>('/super-admin/archivio-documenti'),
    enabled: abilitato,
  })
}

export function scaricaDocumentoArchiviato(d: Pick<DocumentoArchiviatoDto, 'id'>, pdf: boolean) {
  return apiScaricaFile(`/super-admin/archivio-documenti/${d.id}/${pdf ? 'pdf' : 'file'}`, pdf ? 'documento.pdf' : 'documento.xml', true)
}

/** Tutti i documenti archiviati di un Cliente in uno zip, per restituirglieli. */
export function scaricaArchivioCliente(clienteId: string) {
  return apiScaricaFile(`/super-admin/archivio-documenti/clienti/${clienteId}/zip`, 'documenti-archiviati.zip', true)
}
