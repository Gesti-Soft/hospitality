import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut } from './client'

export const TipoNotifica = {
  NuovaPrenotazione: 1,
  PrenotazioneAnnullata: 2,
  PrenotazioneModificata: 3,
  LicenzaInScadenza: 4,
  LicenzaScaduta: 5,
  SchedineInviate: 6,
  CheckOutDimenticato: 7,
} as const
export type TipoNotifica = (typeof TipoNotifica)[keyof typeof TipoNotifica]

export interface NotificaDto {
  id: string
  tipo: TipoNotifica
  titolo: string
  messaggio: string
  prenotazioneId: string | null
  canale: string | null
  createdAtUtc: string
  lettaAtUtc: string | null
}

// Stesso intervallo di refetch già usato dal Calendario (INTERVALLO_REFETCH_PRENOTAZIONI): un
// ritardo fino a un minuto nel vedere una nuova notifica è accettabile, coerente col fatto che il
// polling Wubook/i job che le generano girano anch'essi ogni minuto/ora.
const INTERVALLO_REFETCH_NOTIFICHE = 60_000

export function useNotifiche(strutturaId: string | null, soloNonLette: boolean) {
  return useQuery({
    queryKey: ['notifiche', strutturaId, soloNonLette],
    queryFn: () => apiGet<NotificaDto[]>(`/strutture/${strutturaId}/notifiche?soloNonLette=${soloNonLette}`),
    enabled: !!strutturaId,
    refetchInterval: INTERVALLO_REFETCH_NOTIFICHE,
  })
}

export function useContoNotificheNonLette(strutturaId: string | null) {
  return useQuery({
    queryKey: ['notifiche', strutturaId, 'conteggio'],
    queryFn: () => apiGet<number>(`/strutture/${strutturaId}/notifiche/non-lette/conteggio`),
    enabled: !!strutturaId,
    refetchInterval: INTERVALLO_REFETCH_NOTIFICHE,
  })
}

export function useSegnaNotificaLetta(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (notificaId: string) => apiPut<void>(`/strutture/${strutturaId}/notifiche/${notificaId}/letta`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifiche', strutturaId] }),
  })
}

export function useSegnaTutteNotificheLette(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiPut<void>(`/strutture/${strutturaId}/notifiche/tutte-lette`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifiche', strutturaId] }),
  })
}
