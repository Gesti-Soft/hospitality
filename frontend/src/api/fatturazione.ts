import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiInviaFile, apiPost, apiPut, apiScaricaBlob, apiScaricaFile, ApiError } from './client'

// Gli enum arrivano sul wire come numeri (vedi commento in api/camere.ts). Valori esatti da
// GestiSoft.Domain.Enums — qui solo il sottoinsieme rilevante per la fatturazione di una
// struttura ricettiva, non l'elenco SDI completo (es. TipoDocumento ha 29 codici nel domain,
// la maggior parte per casi reverse-charge/autofattura mai usati da un albergo).
export const RegimeFiscale = {
  RF01_Ordinario: 1,
  RF02_ContribuentiMinimi: 2,
  RF19_Forfettario: 19,
} as const
export type RegimeFiscale = (typeof RegimeFiscale)[keyof typeof RegimeFiscale]

export const TipoDocumentoFattura = {
  TD01_Fattura: 1,
  TD04_NotaDiCredito: 4,
  TD06_Parcella: 6,
} as const
export type TipoDocumentoFattura = (typeof TipoDocumentoFattura)[keyof typeof TipoDocumentoFattura]

export const AliquotaIva = { Iva0: 0, Iva4: 4, Iva5: 5, Iva10: 10, Iva22: 22 } as const
export type AliquotaIva = (typeof AliquotaIva)[keyof typeof AliquotaIva]

export const NaturaIva = {
  N1_EscluseArt15: 1,
  N2_2_NonSoggetteAltriCasi: 22,
  N4_Esenti: 4,
} as const
export type NaturaIva = (typeof NaturaIva)[keyof typeof NaturaIva]

// ---------------------------------------------------------------------------
// Dati aziendali (profilo fiscale emittente, una riga per struttura)
// ---------------------------------------------------------------------------

export interface DatiAziendaliDto {
  strutturaId: string
  iso2: string | null
  pIva: string | null
  codiceFiscale: string | null
  denominazione: string | null
  nome: string | null
  cognome: string | null
  regimeFiscale: RegimeFiscale | null
  /** Aliquota e natura proposte su una fattura nuova: dipendono dal regime di chi emette, non dalla singola fattura. */
  aliquotaIvaDefault: AliquotaIva | null
  naturaDefault: NaturaIva | null
  /** Frase di legge da stampare in fattura quando l'IVA non si applica: la detta il commercialista. */
  dicituraFattura: string | null
  /** Il logo non viaggia nel DTO: si scarica a parte con useLogoDatiAziendali, altrimenti ogni lettura dei dati fiscali si porterebbe dietro un'immagine. */
  haLogo: boolean
  indirizzo: string | null
  nCivico: string | null
  cap: string | null
  comune: string | null
  provincia: string | null
  nazione: string | null
  /** Indirizzo dell'immobile dato in locazione, stampato sulla ricevuta: spesso non è quello del locatore. */
  indirizzoImmobile: string | null
  /** Il locatore ha optato per la cedolare secca: la ricevuta lo dichiara. */
  cedolareSecca: boolean
}

export type DatiAziendaliRequest = Omit<DatiAziendaliDto, 'strutturaId' | 'haLogo'>

export function useDatiAziendali(strutturaId: string | null) {
  return useQuery({
    queryKey: ['dati-aziendali', strutturaId],
    queryFn: () => apiGet<DatiAziendaliDto>(`/strutture/${strutturaId}/dati-aziendali`),
    enabled: !!strutturaId,
  })
}

export function useAggiornaDatiAziendali(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: DatiAziendaliRequest) => apiPut<DatiAziendaliDto>(`/strutture/${strutturaId}/dati-aziendali`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dati-aziendali', strutturaId] }),
  })
}

/**
 * Il logo come data URL, pronto per un `<img src>`: l'immagine sta dietro un endpoint autenticato,
 * quindi non e' indirizzabile direttamente dal browser e va scaricata con il token in mano.
 */
export function useLogoDatiAziendali(strutturaId: string | null, haLogo: boolean) {
  return useQuery({
    queryKey: ['dati-aziendali-logo', strutturaId],
    queryFn: async () => {
      const blob = await apiScaricaBlob(`/strutture/${strutturaId}/dati-aziendali/logo`)
      if (!blob) {
        return null
      }

      return await new Promise<string>((risolvi, rifiuta) => {
        const lettore = new FileReader()
        lettore.onload = () => risolvi(lettore.result as string)
        lettore.onerror = () => rifiuta(new Error('logo non leggibile'))
        lettore.readAsDataURL(blob)
      })
    },
    enabled: !!strutturaId && haLogo,
  })
}

export function useCaricaLogoDatiAziendali(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => apiInviaFile<DatiAziendaliDto>('PUT', `/strutture/${strutturaId}/dati-aziendali/logo`, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['dati-aziendali', strutturaId] })
      void queryClient.invalidateQueries({ queryKey: ['dati-aziendali-logo', strutturaId] })
    },
  })
}

export function useRimuoviLogoDatiAziendali(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiDelete<void>(`/strutture/${strutturaId}/dati-aziendali/logo`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['dati-aziendali', strutturaId] })
      // Svuotata a mano e non invalidata: senza logo la query resta disabilitata, quindi non
      // rifarebbe la richiesta e l'anteprima continuerebbe a mostrare l'immagine appena tolta.
      queryClient.setQueryData(['dati-aziendali-logo', strutturaId], null)
    },
  })
}

// ---------------------------------------------------------------------------
// Dati cliente (anagrafica clienti fatturabili)
// ---------------------------------------------------------------------------

export interface DatiClienteDto {
  id: string
  strutturaId: string
  iso2: string | null
  pIva: string | null
  codiceFiscale: string | null
  denominazione: string | null
  nome: string | null
  cognome: string | null
  /** Formato "YYYY-MM-DDTHH:mm:ss" — solo per suggerire in automatico il Codice Fiscale, non compare in fattura. */
  dataNascita: string | null
  sesso: number | null
  luogoNascita: string | null
  indirizzo: string | null
  nCivico: string | null
  cap: string | null
  luogoResidenza: string | null
  provincia: string | null
  cittadinanza: string | null
  codiceDestinatario: string | null
  pec: string | null
  customerKey: string | null
}

/**
 * `customerKey` resta nel payload (mai un campo editabile in UI) solo per il caso in cui questa
 * richiesta persista per la prima volta un Cliente bare-bones proposto da "Genera fattura" — vedi
 * DatiClienteDialog.
 */
export type DatiClienteRequest = Omit<DatiClienteDto, 'id' | 'strutturaId'>

export function useDatiClienti(strutturaId: string | null) {
  return useQuery({
    queryKey: ['dati-cliente', strutturaId],
    queryFn: () => apiGet<DatiClienteDto[]>(`/strutture/${strutturaId}/dati-cliente`),
    enabled: !!strutturaId,
  })
}

function useInvalidaDatiClienti(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['dati-cliente', strutturaId] })
}

export function useCreaDatiCliente(strutturaId: string | null) {
  const invalida = useInvalidaDatiClienti(strutturaId)
  return useMutation({
    mutationFn: (request: DatiClienteRequest) => apiPost<DatiClienteDto>(`/strutture/${strutturaId}/dati-cliente`, request),
    onSuccess: invalida,
  })
}

export function useAggiornaDatiCliente(strutturaId: string | null) {
  const invalida = useInvalidaDatiClienti(strutturaId)
  return useMutation({
    mutationFn: ({ clienteId, request }: { clienteId: string; request: DatiClienteRequest }) =>
      apiPut<DatiClienteDto>(`/strutture/${strutturaId}/dati-cliente/${clienteId}`, request),
    onSuccess: invalida,
  })
}

// ---------------------------------------------------------------------------
// Fatture
// ---------------------------------------------------------------------------

/**
 * Che documento si emette: dipende da chi ospita, non da una preferenza di stampa, e le due serie
 * hanno numerazione separata. `Fattura` per chi ha partita IVA (va allo SdI, porta aliquota o
 * natura), `Ricevuta` per la locazione breve di un privato (fuori campo IVA, nessun file per lo
 * SdI, bollo sopra 77,47 €).
 */
export const TipoEmissioneDocumento = { Fattura: 1, Ricevuta: 2 } as const
export type TipoEmissioneDocumento = (typeof TipoEmissioneDocumento)[keyof typeof TipoEmissioneDocumento]

/** Come ha pagato l'ospite: si indica e si stampa solo sulla ricevuta di locazione breve. */
export const ModalitaPagamento = { Contanti: 1, Bonifico: 2, CartaDiPagamento: 3, Assegno: 4, PortaleOnline: 5 } as const
export type ModalitaPagamento = (typeof ModalitaPagamento)[keyof typeof ModalitaPagamento]

export const ETICHETTA_MODALITA_PAGAMENTO: Record<ModalitaPagamento, string> = {
  [ModalitaPagamento.Contanti]: 'Contanti',
  [ModalitaPagamento.Bonifico]: 'Bonifico',
  [ModalitaPagamento.CartaDiPagamento]: 'Carta di pagamento',
  [ModalitaPagamento.Assegno]: 'Assegno',
  [ModalitaPagamento.PortaleOnline]: 'Portale di prenotazione',
}

export interface DatiFatturaDto {
  id: string
  strutturaId: string
  datiClienteId: string | null
  clienteNome: string | null
  progressivo: number
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  tipoEmissione: TipoEmissioneDocumento
  modalitaPagamento: ModalitaPagamento | null
  numeroDocumento: number
  dataDocumento: string
  divisa: string | null
  /** Imponibile: somma delle righe, senza IVA né imposta di soggiorno. */
  prezzoTotale: number
  /** IVA di tutte le aliquote. */
  imposta: number
  importoTotale: number
  /** Imposta di soggiorno riaddebitata: in fattura è una riga a sé, esclusa art. 15 (natura N1). */
  impostaSoggiorno: number | null
  /** Bollo virtuale da 2 €, calcolato dal server sulle sole somme non soggette a IVA sopra 77,47 €. */
  importoBollo: number | null
  anno: number
  righe: RigaFatturaDto[]
  /** Le prenotazioni fatturate: la prima è di chi paga. */
  prenotazioneIds: string[]
  /** Stato verso lo SdI; null per ricevute e fatture emesse prima dell'invio dal gestionale. */
  statoSdi: StatoSdiFattura | null
  identificativoSdi: string | null
  inviataSdiAtUtc: string | null
  esitoSdiAtUtc: string | null
  /** Esito dell'ultima ricevuta: per uno scarto, codici e descrizione degli errori. */
  notaSdi: string | null
  /** Su una nota di credito: la fattura che storna. */
  fatturaStornataId: string | null
  /** False quando la fattura è nello SdI: si corregge solo con una nota di credito. */
  modificabile: boolean
  /** Qualcuno ne ha scaricato l'XML per caricarlo da sé sul portale. Valorizzato solo nell'elenco. */
  xmlScaricato: boolean
}

export const StatoSdiFattura = { DaInviare: 1, Inviata: 2, Consegnata: 3, NonConsegnata: 4, Scartata: 5 } as const
export type StatoSdiFattura = (typeof StatoSdiFattura)[keyof typeof StatoSdiFattura]

/**
 * Etichetta e colore di ogni stato. Non consegnata è ambra e non rossa: la fattura è valida, sta
 * nel cassetto fiscale del cliente. Rossa è solo la scartata, l'unico vero rifiuto.
 */
export const STATO_SDI: Record<StatoSdiFattura, { etichetta: string; colore: 'default' | 'info' | 'success' | 'warning' | 'error'; spiegazione: string }> = {
  [StatoSdiFattura.DaInviare]: { etichetta: 'Da inviare', colore: 'default', spiegazione: 'Non ancora trasmessa allo SdI.' },
  [StatoSdiFattura.Inviata]: { etichetta: 'Inviata', colore: 'info', spiegazione: "Lo SdI l'ha presa in carico: si attende la ricevuta." },
  [StatoSdiFattura.Consegnata]: { etichetta: 'Consegnata', colore: 'success', spiegazione: "Il cliente l'ha ricevuta." },
  [StatoSdiFattura.NonConsegnata]: {
    etichetta: 'Non consegnata',
    colore: 'warning',
    spiegazione: 'La fattura è valida: è nel cassetto fiscale del cliente. Basta consegnargli la copia PDF.',
  },
  [StatoSdiFattura.Scartata]: {
    etichetta: 'Scartata',
    colore: 'error',
    spiegazione: "Lo SdI l'ha rifiutata: correggila e inviala di nuovo, con lo stesso numero.",
  },
}

/** Una nota di credito (TD04) che storna in tutto o in parte una fattura. */
export interface CreaNotaDiCreditoRequest {
  righe: RigaFatturaRichiesta[]
  impostaSoggiorno?: number | null
}

/** Da dove viene una riga: il soggiorno o un servizio extra si fatturano una volta sola; una riga a mano è Altro. */
export const TipoRigaFattura = { Soggiorno: 1, Servizio: 2, Altro: 3 } as const
export type TipoRigaFattura = (typeof TipoRigaFattura)[keyof typeof TipoRigaFattura]

/** Una riga del documento (o proposta dalla prenotazione, con numero 0). Aliquota 0% sempre insieme alla natura. */
export interface RigaFatturaDto {
  numero: number
  descrizione: string
  quantita: number
  prezzoUnitario: number
  prezzoTotale: number
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  tipo: TipoRigaFattura
  prenotazioneId: string | null
  prenotazioneServizioId: string | null
}

export interface RigaFatturaRichiesta {
  descrizione: string
  quantita: number
  prezzoUnitario: number
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  tipo: TipoRigaFattura
  prenotazioneId: string | null
  prenotazioneServizioId: string | null
}

export interface PropostaFatturaDto {
  righe: RigaFatturaDto[]
  impostaSoggiorno: number
  /** Da mostrare all'operatore: cauzione esclusa, soggiorno o servizi già fatturati, servizi in una ricevuta. */
  avvisi: string[]
}

export interface CreaFatturaDaPrenotazioneRequest {
  /** La prima è la prenotazione di chi paga: il documento è intestato a lui. */
  prenotazioneIds: string[]
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  divisa: string | null
  righe: RigaFatturaRichiesta[]
  /** Omessa o zero: l'imposta di soggiorno non viene riaddebitata in fattura. */
  impostaSoggiorno?: number | null
  /** Assente = fattura. */
  tipoEmissione?: TipoEmissioneDocumento
  modalitaPagamento?: ModalitaPagamento | null
  /** A chi è intestato il documento se non all'ospite di chi paga (es. una ditta). Assente = l'ospite. */
  datiClienteId?: string | null
}

export interface AggiornaFatturaRequest {
  datiClienteId: string | null
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  divisa: string | null
  righe: RigaFatturaRichiesta[]
  impostaSoggiorno?: number | null
  modalitaPagamento?: ModalitaPagamento | null
}

/**
 * Righe proposte per fatturare le prenotazioni scelte: il soggiorno e i servizi extra non ancora
 * fatturati, l'imposta di soggiorno e gli avvisi. Non scrive nulla.
 */
export function usePropostaFattura(strutturaId: string | null, prenotazioneIds: string[], tipoEmissione: TipoEmissioneDocumento, abilitata: boolean) {
  return useQuery({
    queryKey: ['fatture', strutturaId, 'proposta', prenotazioneIds.join(','), tipoEmissione],
    queryFn: () =>
      apiGet<PropostaFatturaDto>(
        `/strutture/${strutturaId}/fatture/proposta?${prenotazioneIds.map((id) => `prenotazioneIds=${id}`).join('&')}&tipoEmissione=${tipoEmissione}`,
      ),
    enabled: abilitata && !!strutturaId && prenotazioneIds.length > 0,
    retry: false,
  })
}

export function useFatture(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['fatture', strutturaId, anno],
    queryFn: () => apiGet<DatiFatturaDto[]>(`/strutture/${strutturaId}/fatture?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

/** Anni con almeno una fattura emessa — per il selettore Anno. */
export function useAnniDisponibiliFatture(strutturaId: string | null) {
  return useQuery({
    queryKey: ['fatture', strutturaId, 'anni'],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/fatture/anni`),
    enabled: !!strutturaId,
  })
}

function useInvalidaFatture(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['fatture', strutturaId] })
    // Chiave generica (non per singola prenotazione): invalida il badge "Fattura generata" della
    // scheda ospiti per qualunque prenotazione, non solo quella appena fatturata qui.
    queryClient.invalidateQueries({ queryKey: ['fattura-per-prenotazione', strutturaId] })
  }
}

/** Nota di credito sulla fattura indicata: nasce collegata all'originale, con il suo numero. */
export function useCreaNotaDiCredito(strutturaId: string | null) {
  const invalida = useInvalidaFatture(strutturaId)
  return useMutation({
    mutationFn: ({ fatturaId, request }: { fatturaId: string; request: CreaNotaDiCreditoRequest }) =>
      apiPost<DatiFatturaDto>(`/strutture/${strutturaId}/fatture/${fatturaId}/nota-di-credito`, request),
    onSuccess: invalida,
  })
}

export function useCreaFattura(strutturaId: string | null) {
  const invalida = useInvalidaFatture(strutturaId)
  return useMutation({
    mutationFn: (request: CreaFatturaDaPrenotazioneRequest) => apiPost<DatiFatturaDto>(`/strutture/${strutturaId}/fatture`, request),
    onSuccess: invalida,
  })
}

/** L'eventuale fattura già generata per una prenotazione — null se non ancora fatturata (o se l'operatore non ha il permesso di consultare le fatture). */
/** "Fattura n. 12/2026" o "Ricevuta n. 3/2026": due serie diverse, il nome giusto conta. */
export function nomeDocumento(f: Pick<DatiFatturaDto, 'tipoEmissione' | 'numeroDocumento' | 'anno'> & Partial<Pick<DatiFatturaDto, 'tipoDocumento'>>): string {
  const nome =
    f.tipoEmissione === TipoEmissioneDocumento.Ricevuta
      ? 'Ricevuta'
      : f.tipoDocumento === TipoDocumentoFattura.TD04_NotaDiCredito
        ? 'Nota di credito'
        : 'Fattura'
  return `${nome} n. ${f.numeroDocumento}/${f.anno}`
}

/** Cosa resta da fatturare di una prenotazione: soggiorno già in un documento, servizi extra non ancora. */
export interface DaFatturareDto {
  soggiornoFatturato: boolean
  serviziDaFatturare: number
  importoServiziDaFatturare: number
}

/** "2 servizi da fatturare (45,00 €)", o null se non manca nulla. */
export function testoDaFatturare(d: DaFatturareDto | null | undefined): string | null {
  if (!d || d.serviziDaFatturare === 0) return null
  const importo = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' }).format(d.importoServiziDaFatturare)
  return `${d.serviziDaFatturare} ${d.serviziDaFatturare === 1 ? 'servizio' : 'servizi'} da fatturare (${importo})`
}

/**
 * Stessa chiave di partenza del documento della prenotazione: emettere una fattura la ricarica.
 * Chi non vede le finanze riceve null (403).
 */
export function useDaFatturare(strutturaId: string | null, prenotazioneId: string | null) {
  return useQuery({
    queryKey: ['fattura-per-prenotazione', strutturaId, prenotazioneId, 'da-fatturare'],
    queryFn: async () => {
      try {
        return await apiGet<DaFatturareDto>(`/strutture/${strutturaId}/fatture/prenotazioni/${prenotazioneId}/da-fatturare`)
      } catch (err) {
        if (err instanceof ApiError && (err.status === 404 || err.status === 403)) return null
        throw err
      }
    },
    enabled: !!strutturaId && !!prenotazioneId,
  })
}

export function useFatturaPerPrenotazione(strutturaId: string | null, prenotazioneId: string | null) {
  return useQuery({
    queryKey: ['fattura-per-prenotazione', strutturaId, prenotazioneId],
    queryFn: async () => {
      try {
        return await apiGet<DatiFatturaDto>(`/strutture/${strutturaId}/fatture/prenotazioni/${prenotazioneId}`)
      } catch (err) {
        if (err instanceof ApiError && (err.status === 404 || err.status === 403)) return null
        throw err
      }
    },
    enabled: !!strutturaId && !!prenotazioneId,
  })
}

export function useAggiornaFattura(strutturaId: string | null) {
  const invalida = useInvalidaFatture(strutturaId)
  return useMutation({
    mutationFn: ({ fatturaId, request }: { fatturaId: string; request: AggiornaFatturaRequest }) =>
      apiPut<DatiFatturaDto>(`/strutture/${strutturaId}/fatture/${fatturaId}`, request),
    onSuccess: invalida,
  })
}

export interface ClienteRisoltoDto {
  cliente: DatiClienteDto
  /** True se non esisteva ancora un Cliente fatturabile per l'ospite di questa prenotazione ed è stato appena creato (bare-bones: solo nome/cognome/residenza/cittadinanza) — va completato con P.IVA/CF/indirizzo/PEC prima di procedere. */
  appenaCreato: boolean
}

/** Risolve (trova o crea) il Cliente fatturabile per una prenotazione PRIMA di creare la fattura vera e propria — stesso Cliente che verrebbe usato comunque da useCreaFattura, mai un duplicato. */
export function useRisolviClientePerPrenotazione(strutturaId: string | null) {
  const invalida = useInvalidaDatiClienti(strutturaId)
  return useMutation({
    mutationFn: (prenotazioneId: string) => apiPost<ClienteRisoltoDto>(`/strutture/${strutturaId}/fatture/prenotazioni/${prenotazioneId}/cliente`, {}),
    onSuccess: invalida,
  })
}

export function scaricaFatturaPdf(strutturaId: string, fatturaId: string, numeroDocumento: number) {
  return apiScaricaFile(`/strutture/${strutturaId}/fatture/${fatturaId}/pdf`, `fattura-${numeroDocumento}.pdf`)
}

/** Il nome del file lo decide il server: IT + codice del trasmittente + progressivo, come lo vuole lo SdI. */
export function scaricaFatturaXml(strutturaId: string, fatturaId: string, numeroDocumento: number) {
  return apiScaricaFile(`/strutture/${strutturaId}/fatture/${fatturaId}/xml`, `fattura-${numeroDocumento}.xml`, true)
}

// ---------------------------------------------------------------------------
// Canale SdI
// ---------------------------------------------------------------------------

/** True se le fatture si spediscono dal canale GestiSoft: concesso dal Super Admin e acceso dalla struttura. */
export function useCanaleSdiAttivo(strutturaId: string | null) {
  return useQuery({
    queryKey: ['fatture', strutturaId, 'canale-sdi'],
    queryFn: () => apiGet<boolean>(`/strutture/${strutturaId}/fatture/canale-sdi`),
    enabled: !!strutturaId,
  })
}

/** Cosa è successo a un singolo file XML della fattura. */
export const EsitoTrasmissioneSdi = { Generato: 1, Inviato: 2, Rifiutato: 3, EsitoIgnoto: 4, Consegnato: 5, NonConsegnato: 6, Scartato: 7 } as const
export type EsitoTrasmissioneSdi = (typeof EsitoTrasmissioneSdi)[keyof typeof EsitoTrasmissioneSdi]

export const ESITO_TRASMISSIONE: Record<EsitoTrasmissioneSdi, { etichetta: string; colore: 'default' | 'info' | 'success' | 'warning' | 'error' }> = {
  [EsitoTrasmissioneSdi.Generato]: { etichetta: 'Scaricato', colore: 'default' },
  [EsitoTrasmissioneSdi.Inviato]: { etichetta: 'Inviato', colore: 'info' },
  [EsitoTrasmissioneSdi.Rifiutato]: { etichetta: 'Non accettato', colore: 'error' },
  [EsitoTrasmissioneSdi.EsitoIgnoto]: { etichetta: 'Esito incerto', colore: 'warning' },
  [EsitoTrasmissioneSdi.Consegnato]: { etichetta: 'Consegnato', colore: 'success' },
  [EsitoTrasmissioneSdi.NonConsegnato]: { etichetta: 'Non consegnato', colore: 'warning' },
  [EsitoTrasmissioneSdi.Scartato]: { etichetta: 'Scartato', colore: 'error' },
}

/** Un file XML generato per la fattura, senza i byte (si scaricano a parte). */
export interface TrasmissioneFatturaDto {
  id: string
  nomeFile: string
  /** Spedito dal canale GestiSoft; false = scaricato per caricarlo a mano. */
  viaCanale: boolean
  /** Spedito allo SdI di collaudo: non conta, la fattura resta da inviare. */
  prova: boolean
  esito: EsitoTrasmissioneSdi
  identificativoSdi: string | null
  createdAtUtc: string
  inviataAtUtc: string | null
  esitoAtUtc: string | null
  /** Per uno scarto: codici e descrizioni degli errori, separati da "; ". */
  nota: string | null
  inviataDa: string | null
  /** I messaggi dello SdI su questo file, dal più vecchio. */
  ricevute: RicevutaSdiFatturaDto[]
}

/** Un messaggio dello SdI su un file (consegna, scarto…). tipo: valori di ETICHETTA_TIPO_RICEVUTA_SDI. */
export interface RicevutaSdiFatturaDto {
  id: string
  tipo: number
  nomeFile: string | null
  ricevutaAtUtc: string
}

export function useTrasmissioniFattura(strutturaId: string | null, fatturaId: string | null) {
  return useQuery({
    queryKey: ['fatture', strutturaId, 'trasmissioni', fatturaId],
    queryFn: () => apiGet<TrasmissioneFatturaDto[]>(`/strutture/${strutturaId}/fatture/${fatturaId}/trasmissioni`),
    enabled: !!strutturaId && !!fatturaId,
  })
}

/** Il file esattamente com'era: per quelli spediti, il file partito, non uno rigenerato. */
export function scaricaXmlTrasmissione(strutturaId: string, fatturaId: string, t: Pick<TrasmissioneFatturaDto, 'id' | 'nomeFile'>) {
  return apiScaricaFile(`/strutture/${strutturaId}/fatture/${fatturaId}/trasmissioni/${t.id}/xml`, t.nomeFile, true)
}

/** La ricevuta dello SdI com'è arrivata, firmata. */
export function scaricaRicevutaSdi(strutturaId: string, fatturaId: string, trasmissioneId: string, r: RicevutaSdiFatturaDto) {
  return apiScaricaFile(
    `/strutture/${strutturaId}/fatture/${fatturaId}/trasmissioni/${trasmissioneId}/ricevute/${r.id}`,
    r.nomeFile ?? 'ricevuta-sdi.xml',
    true,
  )
}

/** La stessa ricevuta in PDF leggibile, per chi non sa aprire l'XML. Fa fede l'XML. */
export function scaricaPdfRicevutaSdi(strutturaId: string, fatturaId: string, trasmissioneId: string, r: RicevutaSdiFatturaDto) {
  return apiScaricaFile(
    `/strutture/${strutturaId}/fatture/${fatturaId}/trasmissioni/${trasmissioneId}/ricevute/${r.id}/pdf`,
    'ricevuta-sdi.pdf',
    true,
  )
}

export interface EsitoInvioSdiDto {
  nomeFile: string
  identificativoSdi: string
  /** Spedita allo SdI di collaudo: la fattura resta da inviare. */
  prova: boolean
}

/** Spedisce la fattura allo SdI. Sempre su richiesta esplicita dell'operatore. */
export function useInviaSdi(strutturaId: string | null) {
  const invalida = useInvalidaFatture(strutturaId)
  return useMutation({
    mutationFn: (fatturaId: string) => apiPost<EsitoInvioSdiDto>(`/strutture/${strutturaId}/fatture/${fatturaId}/invia-sdi`, {}),
    // Anche se fallisce: un esito incerto lascia comunque un file nello storico.
    onSettled: invalida,
  })
}

/** Un errore di scarto: codice SdI, descrizione e, se lo SdI lo dà, il suggerimento per correggerlo. */
export interface ErroreScartoSdi {
  codice: string | null
  descrizione: string
  suggerimento: string | null
}

/** Riapre la nota scritta dal server ("00404 Fattura duplicata (suggerimento); …") in un errore per riga. */
export function erroriScarto(nota: string | null): ErroreScartoSdi[] {
  if (!nota) return []
  return nota
    .split('; ')
    .map((parte) => parte.trim())
    .filter(Boolean)
    .map((parte) => {
      const codice = /^(\d{5})\s+/.exec(parte)
      let testo = codice ? parte.slice(codice[0].length) : parte
      let suggerimento: string | null = null
      const tra = /\s\(([^()]*)\)$/.exec(testo)
      if (tra) {
        suggerimento = tra[1]
        testo = testo.slice(0, tra.index)
      }
      return { codice: codice?.[1] ?? null, descrizione: testo, suggerimento }
    })
}

// ---------------------------------------------------------------------------
// Fatture ricevute (dai fornitori, via SdI)
// ---------------------------------------------------------------------------

/** Una fattura di un fornitore. È del Cliente: da ogni struttura si vedono quelle di tutte. Importi del fornitore, mai ricalcolati. */
export interface FatturaRicevutaDto {
  id: string
  strutturaId: string
  strutturaNome: string
  identificativoSdi: string
  fornitore: string
  fornitorePartitaIva: string | null
  fornitoreCodiceFiscale: string | null
  /** TD01, TD04…: com'è scritto nel file. */
  tipoDocumento: string
  /** Il numero del fornitore, testo libero. */
  numero: string
  /** "YYYY-MM-DD" */
  data: string
  divisa: string
  importoTotale: number | null
  imponibile: number
  imposta: number
  ricevutaAtUtc: string
  /** Null finché nessuno l'ha aperta. */
  lettaAtUtc: string | null
}

const TIPI_DOCUMENTO_RICEVUTO: Record<string, string> = {
  TD01: 'Fattura',
  TD02: 'Acconto su fattura',
  TD04: 'Nota di credito',
  TD05: 'Nota di debito',
  TD06: 'Parcella',
  TD24: 'Fattura differita',
}

export function nomeTipoDocumentoRicevuto(codice: string): string {
  return TIPI_DOCUMENTO_RICEVUTO[codice] ?? codice
}

export function useFattureRicevute(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['fatture-ricevute', strutturaId, anno],
    queryFn: () => apiGet<FatturaRicevutaDto[]>(`/strutture/${strutturaId}/fatture-ricevute?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

export function useAnniFattureRicevute(strutturaId: string | null) {
  return useQuery({
    queryKey: ['fatture-ricevute', strutturaId, 'anni'],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/fatture-ricevute/anni`),
    enabled: !!strutturaId,
  })
}

/** Il file com'è arrivato dallo SdI (XML o .p7m). Scaricarlo la segna come letta. */
export function scaricaFatturaRicevuta(strutturaId: string, f: Pick<FatturaRicevutaDto, 'id' | 'numero'>) {
  return apiScaricaFile(`/strutture/${strutturaId}/fatture-ricevute/${f.id}/file`, `fattura-${f.numero}.xml`, true)
}
