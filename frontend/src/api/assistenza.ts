import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiInviaForm, apiPut } from './client'

export const StatoTicket = {
  Aperto: 1,
  Chiuso: 2,
} as const
export type StatoTicket = (typeof StatoTicket)[keyof typeof StatoTicket]

export interface TicketDto {
  id: string
  numero: number
  strutturaId: string
  strutturaNome: string
  clienteRagioneSociale: string
  oggetto: string
  stato: StatoTicket
  createdAtUtc: string
  ultimoMessaggioAtUtc: string
  ultimoMessaggioDaStaff: boolean
  /** Calcolato su chi chiede: per la struttura una risposta dello staff, per lo staff un messaggio della struttura. */
  nonLetto: boolean
  chiusoAtUtc: string | null
  /** Valorizzato 12 mesi dopo la chiusura: messaggi e foto cancellati, restano numero e oggetto. */
  anonimizzatoAtUtc: string | null
}

export interface TicketAllegatoDto {
  id: string
  nomeFile: string
  dimensioneByte: number
  eliminato: boolean
}

export interface TicketMessaggioDto {
  id: string
  daStaff: boolean
  autore: string
  testo: string
  createdAtUtc: string
  allegati: TicketAllegatoDto[]
}

export interface TicketDettaglioDto {
  ticket: TicketDto
  messaggi: TicketMessaggioDto[]
}

// Stessi limiti di AssistenzaService lato Api: controllati anche qui per dirlo prima di caricare.
export const ALLEGATI_MAX_PER_MESSAGGIO = 5
export const ALLEGATO_MAX_BYTE = 5 * 1024 * 1024
export const OGGETTO_MAX_CARATTERI = 150
export const TESTO_MAX_CARATTERI = 5000
export const TIPI_FOTO_ACCETTATI = 'image/png,image/jpeg,image/webp'

// Come le notifiche: una risposta vista con un minuto di ritardo va bene.
const INTERVALLO_REFETCH_ASSISTENZA = 60_000

function formMessaggio(testo: string, allegati: File[], oggetto?: string): FormData {
  const form = new FormData()
  if (oggetto !== undefined) form.append('oggetto', oggetto)
  form.append('testo', testo)
  for (const file of allegati) form.append('allegati', file)
  return form
}

/**
 * Da dove si leggono i ticket: la struttura selezionata (titolare e chi gestisce gli utenti) oppure
 * il pannello Super Admin, che li vede tutti. Le due pagine condividono componenti e chiamate,
 * cambia solo il prefisso.
 */
export type ContestoAssistenza = { tipo: 'struttura'; strutturaId: string } | { tipo: 'staff' }

function base(contesto: ContestoAssistenza): string {
  return contesto.tipo === 'staff' ? '/super-admin/assistenza/ticket' : `/strutture/${contesto.strutturaId}/assistenza/ticket`
}

function chiave(contesto: ContestoAssistenza): unknown[] {
  return contesto.tipo === 'staff' ? ['assistenza', 'staff'] : ['assistenza', 'struttura', contesto.strutturaId]
}

export function percorsoAllegato(contesto: ContestoAssistenza, allegatoId: string): string {
  return `${base(contesto)}/allegati/${allegatoId}`
}

export function useTickets(contesto: ContestoAssistenza | null, soloAperti = false) {
  return useQuery({
    queryKey: [...(contesto ? chiave(contesto) : ['assistenza']), 'lista', soloAperti],
    queryFn: () => apiGet<TicketDto[]>(contesto!.tipo === 'staff' ? `${base(contesto!)}?soloAperti=${soloAperti}` : base(contesto!)),
    enabled: !!contesto,
    refetchInterval: INTERVALLO_REFETCH_ASSISTENZA,
  })
}

export function useContoTicketNonLetti(contesto: ContestoAssistenza | null) {
  return useQuery({
    queryKey: [...(contesto ? chiave(contesto) : ['assistenza']), 'conteggio'],
    queryFn: () => apiGet<number>(`${base(contesto!)}/non-letti/conteggio`),
    enabled: !!contesto,
    refetchInterval: INTERVALLO_REFETCH_ASSISTENZA,
  })
}

export function useTicket(contesto: ContestoAssistenza, ticketId: string | null) {
  return useQuery({
    queryKey: [...chiave(contesto), 'dettaglio', ticketId],
    queryFn: () => apiGet<TicketDettaglioDto>(`${base(contesto)}/${ticketId}`),
    enabled: !!ticketId,
    refetchInterval: INTERVALLO_REFETCH_ASSISTENZA,
  })
}

export function useSegnaTicketLetto(contesto: ContestoAssistenza) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ticketId: string) => apiPut<void>(`${base(contesto)}/${ticketId}/letto`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chiave(contesto) }),
  })
}

export function useApriTicket(strutturaId: string) {
  const queryClient = useQueryClient()
  const contesto: ContestoAssistenza = { tipo: 'struttura', strutturaId }
  return useMutation({
    mutationFn: ({ oggetto, testo, allegati }: { oggetto: string; testo: string; allegati: File[] }) =>
      apiInviaForm<TicketDettaglioDto>('POST', base(contesto), formMessaggio(testo, allegati, oggetto)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chiave(contesto) }),
  })
}

export function useRispondiTicket(contesto: ContestoAssistenza) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ ticketId, testo, allegati }: { ticketId: string; testo: string; allegati: File[] }) =>
      apiInviaForm<TicketDettaglioDto>('POST', `${base(contesto)}/${ticketId}/messaggi`, formMessaggio(testo, allegati)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chiave(contesto) }),
  })
}

export function useChiudiTicket() {
  const queryClient = useQueryClient()
  const contesto: ContestoAssistenza = { tipo: 'staff' }
  return useMutation({
    mutationFn: (ticketId: string) => apiPut<TicketDettaglioDto>(`${base(contesto)}/${ticketId}/chiudi`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chiave(contesto) }),
  })
}
