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

export interface DatiFatturaDto {
  id: string
  strutturaId: string
  datiClienteId: string | null
  clienteNome: string | null
  progressivo: number
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  tipoEmissione: TipoEmissioneDocumento
  numeroDocumento: number
  dataDocumento: string
  divisa: string | null
  descrizione: string | null
  quantita: number
  prezzoUnitario: number
  prezzoTotale: number
  importoTotale: number
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  /** Imposta di soggiorno riaddebitata: in fattura è una riga a sé, esclusa art. 15 (natura N1). */
  impostaSoggiorno: number | null
  /** Bollo virtuale da 2 €, calcolato dal server sulle sole somme non soggette a IVA sopra 77,47 €. */
  importoBollo: number | null
  anno: number
}

export interface CreaFatturaDaPrenotazioneRequest {
  prenotazioneId: string
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  descrizione: string | null
  quantita: number
  prezzoUnitario: number | null
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  divisa: string | null
  /** Omessa o zero: l'imposta di soggiorno non viene riaddebitata in fattura. */
  impostaSoggiorno?: number | null
  /** Assente = fattura. */
  tipoEmissione?: TipoEmissioneDocumento
}

export interface AggiornaFatturaRequest {
  datiClienteId: string | null
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
  descrizione: string | null
  quantita: number
  prezzoUnitario: number
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  divisa: string | null
  impostaSoggiorno?: number | null
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

export function useCreaFattura(strutturaId: string | null) {
  const invalida = useInvalidaFatture(strutturaId)
  return useMutation({
    mutationFn: (request: CreaFatturaDaPrenotazioneRequest) => apiPost<DatiFatturaDto>(`/strutture/${strutturaId}/fatture`, request),
    onSuccess: invalida,
  })
}

/** L'eventuale fattura già generata per una prenotazione — null se non ancora fatturata (o se l'operatore non ha il permesso di consultare le fatture). */
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

export function scaricaFatturaXml(strutturaId: string, fatturaId: string, numeroDocumento: number) {
  return apiScaricaFile(`/strutture/${strutturaId}/fatture/${fatturaId}/xml`, `fattura-${numeroDocumento}.xml`)
}
