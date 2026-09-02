import { useMutation, useQuery } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'

export interface StrutturaDto {
  id: string
  clienteId: string
  nome: string
  createdAtUtc: string
  /** Concessi dal Super Admin — se false la relativa schermata non deve comparire, indipendentemente dai toggle self-service. */
  wubookAbilitato: boolean
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
}

export function useStrutture(clienteId: string | null, abilitato = true) {
  return useQuery({
    queryKey: ['strutture', clienteId ?? 'proprie'],
    queryFn: () => apiGet<StrutturaDto[]>(clienteId ? `/strutture?clienteId=${clienteId}` : '/strutture'),
    enabled: abilitato,
  })
}

/**
 * Un Cliente crea la struttura per sé stesso (il backend deduce il ClienteId dall'utente
 * autenticato, clienteId va omesso/null); un Super Admin deve specificare per quale Cliente.
 */
export function useCreaStruttura() {
  return useMutation({
    mutationFn: ({ nome, clienteId }: { nome: string; clienteId?: string | null }) =>
      apiPost<StrutturaDto>('/strutture', { clienteId: clienteId ?? null, nome }),
  })
}

export function useAggiornaStruttura() {
  return useMutation({
    mutationFn: ({ strutturaId, nome }: { strutturaId: string; nome: string }) =>
      apiPut<StrutturaDto>(`/strutture/${strutturaId}`, { nome }),
  })
}

/** "Elimina" una struttura: in realtà una disattivazione (soft-delete), nessun dato viene perso. */
export function useImpostaAttivoStruttura() {
  return useMutation({
    mutationFn: ({ strutturaId, attivo }: { strutturaId: string; attivo: boolean }) =>
      apiPut(`/strutture/${strutturaId}/attivo`, { attivo }),
  })
}
