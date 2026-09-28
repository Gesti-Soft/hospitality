import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { ModalitaPagamento } from './fatturazione'

// Come gli altri enum: l'Api li serializza come numeri.
export const TipoPagamento = { Acconto: 1, Caparra: 2, Saldo: 3, Altro: 4, Rimborso: 5 } as const
export type TipoPagamento = (typeof TipoPagamento)[keyof typeof TipoPagamento]

export const ETICHETTA_TIPO_PAGAMENTO: Record<TipoPagamento, string> = {
  [TipoPagamento.Acconto]: 'Acconto',
  [TipoPagamento.Caparra]: 'Caparra',
  [TipoPagamento.Saldo]: 'Saldo',
  [TipoPagamento.Altro]: 'Altro',
  [TipoPagamento.Rimborso]: 'Rimborso',
}

/** Un incasso o un rimborso. Importo sempre positivo: il rimborso si sottrae. Data "YYYY-MM-DD". */
export interface PagamentoPrenotazioneDto {
  id: string
  data: string
  importo: number
  tipo: TipoPagamento
  /** Null solo per gli importi registrati prima del registro dei pagamenti. */
  metodo: ModalitaPagamento | null
  nota: string | null
  registratoDa: string | null
  registratoIlUtc: string
}

export interface SalvaPagamentoRequest {
  data: string
  importo: number
  tipo: TipoPagamento
  metodo: ModalitaPagamento | null
  nota: string | null
}

/** Incassi meno rimborsi, in centesimi per non sbagliare arrotondamento. */
export function nettoPagamenti(righe: { importo: number; tipo: TipoPagamento }[]): number {
  const centesimi = righe.reduce((somma, r) => somma + (r.tipo === TipoPagamento.Rimborso ? -1 : 1) * Math.round(r.importo * 100), 0)
  return centesimi / 100
}

const chiave = (strutturaId: string | null, prenotazioneId: string | null) => ['pagamenti', strutturaId, prenotazioneId] as const

export function usePagamenti(strutturaId: string | null, prenotazioneId: string | null) {
  return useQuery({
    queryKey: chiave(strutturaId, prenotazioneId),
    queryFn: () => apiGet<PagamentoPrenotazioneDto[]>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/pagamenti`),
    enabled: !!strutturaId && !!prenotazioneId,
  })
}

function useInvalida(strutturaId: string | null, prenotazioneId: string | null) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: chiave(strutturaId, prenotazioneId) })
    // Il pagato della prenotazione (liste, calendario) e la Cassa cambiano con il registro.
    queryClient.invalidateQueries({ queryKey: ['prenotazioni', strutturaId] })
    queryClient.invalidateQueries({ queryKey: ['finanze', strutturaId] })
  }
}

export function useRegistraPagamento(strutturaId: string | null, prenotazioneId: string | null) {
  const invalida = useInvalida(strutturaId, prenotazioneId)
  return useMutation({
    mutationFn: (pagamento: SalvaPagamentoRequest) =>
      apiPost<PagamentoPrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/pagamenti`, pagamento),
    onSuccess: invalida,
  })
}

export function useAggiornaPagamento(strutturaId: string | null, prenotazioneId: string | null) {
  const invalida = useInvalida(strutturaId, prenotazioneId)
  return useMutation({
    mutationFn: ({ id, ...pagamento }: SalvaPagamentoRequest & { id: string }) =>
      apiPut<PagamentoPrenotazioneDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/pagamenti/${id}`, pagamento),
    onSuccess: invalida,
  })
}

export function useEliminaPagamento(strutturaId: string | null, prenotazioneId: string | null) {
  const invalida = useInvalida(strutturaId, prenotazioneId)
  return useMutation({
    mutationFn: (id: string) => apiDelete<void>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/pagamenti/${id}`),
    onSuccess: invalida,
  })
}
