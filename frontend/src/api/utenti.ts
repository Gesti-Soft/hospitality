import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'

// Come gli altri enum: l'Api li serializza come numeri (vedi commento in api/camere.ts).
export const RuoloUtente = {
  Administrator: 1,
  Receptionist: 2,
  Housekeeper: 3,
  Manager: 4,
  Accountant: 5,
  Maintenance: 6,
  FnbManager: 7,
  BookingAgent: 8,
  NightAuditor: 9,
  Marketing: 10,
  Owner: 11,
} as const
export type RuoloUtente = (typeof RuoloUtente)[keyof typeof RuoloUtente]

export interface UtenteDto {
  id: string
  email: string
  nome: string | null
  cognome: string | null
  isSuperAdmin: boolean
  clienteId: string | null
  attivo: boolean
}

export interface PermessiStruttura {
  bookingRead: boolean
  bookingWrite: boolean
  reservationRead: boolean
  reservationWrite: boolean
  statePoliceRead: boolean
  statePoliceWrite: boolean
  statePoliceSettings: boolean
  settingAgency: boolean
  settingUser: boolean
  settingRoomRead: boolean
  settingRoomWrite: boolean
  roomStatusUpdate: boolean
  financeRead: boolean
  financeWrite: boolean
  restaurantRead: boolean
  restaurantWrite: boolean
}

export interface AssegnazioneStrutturaDto extends PermessiStruttura {
  id: string
  utenteId: string
  email: string
  nome: string | null
  cognome: string | null
  strutturaId: string
  ruolo: RuoloUtente
}

export interface CreaUtenteRequest {
  email: string
  password: string
  nome: string | null
  cognome: string | null
  isSuperAdmin: boolean
  clienteId: string | null
}

export interface AssegnaRuoloRequest extends PermessiStruttura {
  ruolo: RuoloUtente
}

export function useUtentiCliente(clienteId: string | null) {
  return useQuery({
    queryKey: ['utenti-cliente', clienteId],
    queryFn: () => apiGet<UtenteDto[]>(`/clienti/${clienteId}/utenti`),
    enabled: !!clienteId,
  })
}

export function useAssegnazioniStruttura(strutturaId: string | null) {
  return useQuery({
    queryKey: ['assegnazioni-struttura', strutturaId],
    queryFn: () => apiGet<AssegnazioneStrutturaDto[]>(`/strutture/${strutturaId}/utenti`),
    enabled: !!strutturaId,
  })
}

export function useCreaUtente() {
  return useMutation({
    mutationFn: (request: CreaUtenteRequest) => apiPost<UtenteDto>('/utenti', request),
  })
}

export function useAssegnaRuolo(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ utenteId, request }: { utenteId: string; request: AssegnaRuoloRequest }) =>
      apiPut(`/utenti/${utenteId}/strutture/${strutturaId}/ruolo`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['assegnazioni-struttura', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['utenti-cliente'] })
    },
  })
}

export function useCambiaPasswordPropria() {
  return useMutation({
    mutationFn: (request: { passwordAttuale: string; passwordNuova: string }) => apiPost<void>('/utenti/me/cambia-password', request),
  })
}
