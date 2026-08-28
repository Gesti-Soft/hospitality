import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPut } from './client'

export interface StrutturaAdminDto {
  id: string
  nome: string
  /** False = struttura "eliminata" (soft-delete) dal Cliente stesso o dal Super Admin. */
  attivo: boolean
  /** Valorizzato solo se !attivo — quando è passato da oltre 90 giorni la struttura è eliminabile definitivamente. */
  disattivataAtUtc: string | null
  wubookAttivo: boolean
  wubookUltimoErrore: string | null
  wubookCacheAggiornataAtUtc: string | null
  poliziaStatoAttiva: boolean
  osservatorioAttivo: boolean
  payTouristAttivo: boolean
  /** Concessi dal Super Admin per QUESTA struttura — distinti dai flag sopra, che sono il toggle self-service del Cliente. */
  wubookAbilitato: boolean
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
}

export interface ClienteAdminDto {
  id: string
  ragioneSociale: string
  partitaIva: string | null
  attivo: boolean
  createdAtUtc: string
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
}

export function useDashboardSuperAdmin(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'dashboard'],
    queryFn: () => apiGet<DashboardSuperAdminDto>('/super-admin/dashboard'),
    enabled: abilitato,
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

export interface AggiornaUtenteRequest {
  email: string
  nome: string | null
  cognome: string | null
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

export const GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA = 90

export function giorniDaDisattivazione(disattivataAtUtc: string): number {
  const ms = Date.now() - new Date(disattivataAtUtc).getTime()
  return Math.floor(ms / (1000 * 60 * 60 * 24))
}
