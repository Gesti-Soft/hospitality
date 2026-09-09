import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'

export interface CanaleVenditaDto {
  id: string
  strutturaId: string
  descrizione: string
  colore: string
}

export function useCanaliVendita(strutturaId: string | null) {
  return useQuery({
    queryKey: ['canali-vendita', strutturaId],
    queryFn: () => apiGet<CanaleVenditaDto[]>(`/strutture/${strutturaId}/canali-vendita`),
    enabled: !!strutturaId,
  })
}

function useInvalidaCanali(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: ['canali-vendita', strutturaId] })
}

export function useCreaCanaleVendita(strutturaId: string | null) {
  const invalida = useInvalidaCanali(strutturaId)
  return useMutation({
    mutationFn: ({ descrizione, colore }: { descrizione: string; colore: string }) =>
      apiPost<CanaleVenditaDto>(`/strutture/${strutturaId}/canali-vendita`, { descrizione, colore }),
    onSuccess: invalida,
  })
}

export function useAggiornaCanaleVendita(strutturaId: string | null) {
  const invalida = useInvalidaCanali(strutturaId)
  return useMutation({
    mutationFn: ({ canaleId, descrizione, colore }: { canaleId: string; descrizione: string; colore: string }) =>
      apiPut<CanaleVenditaDto>(`/strutture/${strutturaId}/canali-vendita/${canaleId}`, { descrizione, colore }),
    onSuccess: invalida,
  })
}

/** Crea un canale per ogni valore di Agenzia già presente sulle prenotazioni (es. da Wubook) che non corrisponde ancora a nessun canale configurato. */
export function useImportaCanaliVendita(strutturaId: string | null) {
  const invalida = useInvalidaCanali(strutturaId)
  return useMutation({
    mutationFn: () => apiPost<CanaleVenditaDto[]>(`/strutture/${strutturaId}/canali-vendita/importa-da-prenotazioni`),
    onSuccess: invalida,
  })
}

export function useEliminaCanaleVendita(strutturaId: string | null) {
  const invalida = useInvalidaCanali(strutturaId)
  return useMutation({
    mutationFn: (canaleId: string) => apiDelete<void>(`/strutture/${strutturaId}/canali-vendita/${canaleId}`),
    onSuccess: invalida,
  })
}
