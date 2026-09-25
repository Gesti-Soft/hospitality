import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut, apiScaricaFile } from './client'
import type { TipoVariazionePrezzo } from './tipologie'

// Come gli altri enum: l'Api li serializza come numeri.
export const TipoTrattamento = { Colazione: 1, MezzaPensione: 2, PensioneCompleta: 3 } as const
export type TipoTrattamento = (typeof TipoTrattamento)[keyof typeof TipoTrattamento]

export const NOME_TRATTAMENTO: Record<TipoTrattamento, string> = {
  [TipoTrattamento.Colazione]: 'Colazione',
  [TipoTrattamento.MezzaPensione]: 'Mezza pensione',
  [TipoTrattamento.PensioneCompleta]: 'Pensione completa',
}

/** Listino di un trattamento della struttura, a persona e a notte. Prezzo bambini null = pagano come gli adulti. */
export interface TrattamentoStrutturaDto {
  tipo: TipoTrattamento
  attivo: boolean
  prezzoPerPersona: number
  prezzoBambini: number | null
  tipoPrezzoBambini: TipoVariazionePrezzo
  /** Fino a questa età compresa si paga il prezzo bambini. */
  etaMassimaBambini: number | null
  /** Chi serve la colazione, stampato sui buoni (es. il bar convenzionato). */
  esercizioConvenzionato: string | null
}

export function useTrattamenti(strutturaId: string | null) {
  return useQuery({
    queryKey: ['trattamenti', strutturaId],
    queryFn: () => apiGet<TrattamentoStrutturaDto[]>(`/strutture/${strutturaId}/trattamenti`),
    enabled: !!strutturaId,
  })
}

export function useSalvaTrattamento(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (trattamento: TrattamentoStrutturaDto) => apiPut<TrattamentoStrutturaDto>(`/strutture/${strutturaId}/trattamenti`, trattamento),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['trattamenti', strutturaId] })
      // Il preventivo di una prenotazione nuova usa il listino.
      queryClient.invalidateQueries({ queryKey: ['preventivo', strutturaId] })
    },
  })
}

export function scaricaBuoniColazione(strutturaId: string, prenotazioneId: string, numeroPrenotazione: string | null) {
  return apiScaricaFile(
    `/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/buoni-colazione`,
    `buoni-colazione${numeroPrenotazione ? `-${numeroPrenotazione}` : ''}.pdf`,
  )
}
