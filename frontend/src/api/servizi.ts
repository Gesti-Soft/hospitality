import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { AliquotaIva, NaturaIva } from './fatturazione'

// Come gli altri enum: l'Api li serializza come numeri.
export const ModalitaPrezzoServizio = { APersonaANotte: 1, APersona: 2, ANotte: 3, APrenotazione: 4 } as const
export type ModalitaPrezzoServizio = (typeof ModalitaPrezzoServizio)[keyof typeof ModalitaPrezzoServizio]

export const NOME_MODALITA: Record<ModalitaPrezzoServizio, string> = {
  [ModalitaPrezzoServizio.APersonaANotte]: 'a persona e a notte',
  [ModalitaPrezzoServizio.APersona]: 'a persona',
  [ModalitaPrezzoServizio.ANotte]: 'a notte',
  [ModalitaPrezzoServizio.APrenotazione]: 'a prenotazione',
}

/** Nei servizi "a persona" la quantità sono le persone, negli altri le unità (es. posti auto). */
export const perPersona = (m: ModalitaPrezzoServizio) => m === ModalitaPrezzoServizio.APersonaANotte || m === ModalitaPrezzoServizio.APersona
export const perNotte = (m: ModalitaPrezzoServizio) => m === ModalitaPrezzoServizio.APersonaANotte || m === ModalitaPrezzoServizio.ANotte

/** Quando è stato venduto: con la prenotazione, o addebitato durante il soggiorno (dopo il check-in). */
export const OrigineServizio = { ConLaPrenotazione: 1, DuranteIlSoggiorno: 2 } as const
export type OrigineServizio = (typeof OrigineServizio)[keyof typeof OrigineServizio]

/** Notti tra due date "YYYY-MM-DD", come tra check-in e check-out. */
export function nottiTra(dal: string, al: string): number {
  const [a1, m1, g1] = dal.split('-').map(Number)
  const [a2, m2, g2] = al.split('-').map(Number)
  return Math.round((Date.UTC(a2, m2 - 1, g2) - Date.UTC(a1, m1 - 1, g1)) / 86_400_000)
}

/**
 * Importo di una riga, solo da mostrare (il conto vero lo fa il preventivo del backend): in centesimi
 * per non sbagliare arrotondamento. A notte conta le notti della riga, non di tutto il soggiorno.
 */
export function importoRiga(modalita: ModalitaPrezzoServizio, prezzo: number, quantita: number, notti: number): number {
  return (Math.round(prezzo * 100) * quantita * (perNotte(modalita) ? notti : 1)) / 100
}

/** Servizio extra del listino (escursione, parcheggio…). Gli eliminati non arrivano. */
export interface ServizioStrutturaDto {
  id: string
  nome: string
  prezzo: number
  modalita: ModalitaPrezzoServizio
  attivo: boolean
  /** Con cui si fattura; entrambe null = quella predefinita della struttura. 0% sempre con la natura. */
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  /** Lo stesso del servizio sul sito web: così le prenotazioni del sito arrivano con i servizi già aggiunti. */
  codice: string | null
}

export interface SalvaServizioRequest {
  nome: string
  prezzo: number
  modalita: ModalitaPrezzoServizio
  attivo: boolean
  aliquotaIva: AliquotaIva | null
  natura: NaturaIva | null
  codice: string | null
}

/**
 * Servizio venduto con una prenotazione: nome e prezzo sono quelli del momento della vendita.
 * Date "YYYY-MM-DD"; `al` solo per i servizi a notte (giorno dopo l'ultima notte, come il check-out).
 */
export interface ServizioPrenotazioneDto {
  id: string
  servizioId: string
  nome: string
  modalita: ModalitaPrezzoServizio
  prezzoUnitario: number
  quantita: number
  dal: string
  al: string | null
  origine: OrigineServizio
  aggiuntoDa: string | null
  aggiuntoIlUtc: string
}

/** `rigaId` per una riga che la prenotazione ha già (tiene il suo prezzo), null per una nuova. */
export interface ServizioPrenotazioneRichiesta {
  rigaId: string | null
  servizioId: string
  quantita: number
  dal: string
  al: string | null
}

export function useServizi(strutturaId: string | null) {
  return useQuery({
    queryKey: ['servizi', strutturaId],
    queryFn: () => apiGet<ServizioStrutturaDto[]>(`/strutture/${strutturaId}/servizi`),
    enabled: !!strutturaId,
  })
}

export function useServiziPrenotazione(strutturaId: string | null, prenotazioneId: string | null) {
  return useQuery({
    queryKey: ['servizi', strutturaId, 'prenotazione', prenotazioneId],
    queryFn: () => apiGet<ServizioPrenotazioneDto[]>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/servizi`),
    enabled: !!strutturaId && !!prenotazioneId,
  })
}

function useInvalidaServizi(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: ['servizi', strutturaId] })
    // Il preventivo di una prenotazione nuova usa il listino.
    queryClient.invalidateQueries({ queryKey: ['preventivo', strutturaId] })
  }
}

export function useCreaServizio(strutturaId: string | null) {
  const invalida = useInvalidaServizi(strutturaId)
  return useMutation({
    mutationFn: (servizio: SalvaServizioRequest) => apiPost<ServizioStrutturaDto>(`/strutture/${strutturaId}/servizi`, servizio),
    onSuccess: invalida,
  })
}

export function useAggiornaServizio(strutturaId: string | null) {
  const invalida = useInvalidaServizi(strutturaId)
  return useMutation({
    mutationFn: ({ id, ...servizio }: SalvaServizioRequest & { id: string }) => apiPut<ServizioStrutturaDto>(`/strutture/${strutturaId}/servizi/${id}`, servizio),
    onSuccess: invalida,
  })
}

export function useEliminaServizio(strutturaId: string | null) {
  const invalida = useInvalidaServizi(strutturaId)
  return useMutation({
    mutationFn: (id: string) => apiDelete<void>(`/strutture/${strutturaId}/servizi/${id}`),
    onSuccess: invalida,
  })
}

/**
 * Addebito rapido dalla reception: la riga si aggiunge e il suo importo va sul totale della
 * prenotazione. Dal dialogo della prenotazione invece il nuovo totale si propone e si conferma.
 */
export function useAddebitaServizio(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ prenotazioneId, ...riga }: ServizioPrenotazioneRichiesta & { prenotazioneId: string }) =>
      apiPost<ServizioPrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/servizi`, riga),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['servizi', strutturaId, 'prenotazione'] })
      queryClient.invalidateQueries({ queryKey: ['prenotazioni', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['preventivo', strutturaId] })
    },
  })
}
