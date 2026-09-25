import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'

/** A che punto è la pulizia (o il cambio biancheria) di una camera occupata. Stessi valori dell'enum StatoServizioSoggiorno. */
export const StatoServizioSoggiorno = {
  NonPrevisto: 1,
  Programmato: 2,
  DaFareOggi: 3,
  InRitardo: 4,
  Rinunciato: 5,
} as const
export type StatoServizioSoggiorno = (typeof StatoServizioSoggiorno)[keyof typeof StatoServizioSoggiorno]

export type ServizioSoggiorno = 'Pulizia' | 'Biancheria'

export interface ServizioSoggiornoDto {
  stato: StatoServizioSoggiorno
  intervalloGiorni: number | null
  previsto: string | null
  ultimoFatto: string | null
}

/** Camera occupata vista da chi la pulisce: niente nome dell'ospite né importi. */
export interface SoggiornoPulizieDto {
  prenotazioneId: string
  cameraNome: string | null
  tipologiaNome: string | null
  numeroOspiti: number | null
  arrivo: string | null
  partenza: string | null
  notteCorrente: number | null
  nottiTotali: number | null
  pulizia: ServizioSoggiornoDto
  biancheria: ServizioSoggiornoDto
}

// Come la lista delle camere da pulire: chi è al piano deve vedere presto un cambio fatto al banco.
const INTERVALLO_REFETCH_PULIZIE = 60_000

export function useSoggiorniPulizie(strutturaId: string | null) {
  return useQuery({
    queryKey: ['pulizie-soggiorni', strutturaId],
    queryFn: () => apiGet<SoggiornoPulizieDto[]>(`/strutture/${strutturaId}/pulizie/soggiorni`),
    enabled: !!strutturaId,
    refetchInterval: INTERVALLO_REFETCH_PULIZIE,
  })
}

export function useSegnaServizioSoggiorno(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ prenotazioneId, servizio }: { prenotazioneId: string; servizio: ServizioSoggiorno }) =>
      apiPost<void>(`/strutture/${strutturaId}/pulizie/soggiorni/${prenotazioneId}/${servizio}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulizie-soggiorni', strutturaId] }),
  })
}

/** Rinunce dell'ospite: si salvano subito, restano nel log con chi le ha registrate. */
export function useAggiornaRinunceServizi(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ prenotazioneId, rinunciaPulizia, rinunciaBiancheria }: { prenotazioneId: string; rinunciaPulizia: boolean; rinunciaBiancheria: boolean }) =>
      apiPut<{ rinunciaPulizia: boolean; rinunciaBiancheria: boolean }>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/rinunce-servizi`, {
        rinunciaPulizia,
        rinunciaBiancheria,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['prenotazioni', strutturaId] })
      queryClient.invalidateQueries({ queryKey: ['pulizie-soggiorni', strutturaId] })
    },
  })
}
