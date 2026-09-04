import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut, apiScaricaFile, ApiError } from './client'

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
  indirizzo: string | null
  nCivico: string | null
  cap: string | null
  comune: string | null
  provincia: string | null
  nazione: string | null
}

export type DatiAziendaliRequest = Omit<DatiAziendaliDto, 'strutturaId'>

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

export type DatiClienteRequest = Omit<DatiClienteDto, 'id' | 'strutturaId' | 'customerKey'>

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

export interface DatiFatturaDto {
  id: string
  strutturaId: string
  datiClienteId: string | null
  clienteNome: string | null
  progressivo: number
  tipoDocumento: TipoDocumentoFattura | null
  regimeFiscale: RegimeFiscale | null
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
}

export function useFatture(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['fatture', strutturaId, anno],
    queryFn: () => apiGet<DatiFatturaDto[]>(`/strutture/${strutturaId}/fatture?anno=${anno}`),
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
