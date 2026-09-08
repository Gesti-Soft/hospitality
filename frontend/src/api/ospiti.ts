import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiGet, apiPut } from './client'

// Come StatoCamera/StatoPrenotazione: l'Api serializza gli enum come numeri.
export const Sesso = { Maschio: 1, Femmina: 2 } as const
export type Sesso = (typeof Sesso)[keyof typeof Sesso]

export interface OspiteRigaDto {
  id: string
  cameraId: string | null
  permanenza: number | null
  dataNascita: string | null
  sesso: Sesso | null
  cognome: string | null
  nome: string | null
  cittadinanza: string | null
  luogoNascita: string | null
  statoNascita: string | null
  luogoResidenza: string | null
  postoLetto: boolean | null
  esenteDaTassa: boolean
}

export interface OspiteDto {
  id: string
  strutturaId: string
  prenotazioneId: string | null
  tipoOspite: string | null
  permanenza: number | null
  dataNascita: string | null
  sesso: Sesso | null
  cognome: string | null
  nome: string | null
  cittadinanza: string | null
  luogoNascita: string | null
  statoNascita: string | null
  luogoResidenza: string | null
  email: string | null
  documento: string | null
  numeroDocumento: string | null
  rilascioDocumento: string | null
  esenteDaTassa: boolean
  membri: OspiteRigaDto[]
}

export interface MembroOspiteRequest {
  id: string | null
  cameraId: string | null
  permanenza: number | null
  dataNascita: string | null
  sesso: Sesso | null
  cognome: string | null
  nome: string | null
  cittadinanza: string | null
  luogoNascita: string | null
  statoNascita: string | null
  luogoResidenza: string | null
  postoLetto: boolean | null
  esenteDaTassa: boolean
}

export interface SalvaSchedaOspitiRequest {
  tipoOspite: string | null
  permanenza: number | null
  dataNascita: string | null
  sesso: Sesso | null
  cognome: string | null
  nome: string | null
  cittadinanza: string | null
  luogoNascita: string | null
  statoNascita: string | null
  luogoResidenza: string | null
  email: string | null
  documento: string | null
  numeroDocumento: string | null
  rilascioDocumento: string | null
  esenteDaTassa: boolean
  membri: MembroOspiteRequest[]
}

/**
 * Una scheda esiste già non appena Wubook sincronizza una prenotazione (autocompila
 * Nome/Cognome/Email/Cittadinanza dal canale OTA, vedi WubookPrenotazioniService) — non basta quindi
 * la sola esistenza del record per dire che è pronta per la schedina Alloggiati Web. Servono anche i
 * dati che Wubook non fornisce mai: data di nascita, sesso, documento (mai popolati dall'import).
 */
export function schedaOspitiCompleta(ospite: OspiteDto): boolean {
  return !!(
    ospite.cognome &&
    ospite.nome &&
    ospite.dataNascita &&
    ospite.sesso &&
    ospite.cittadinanza &&
    ospite.documento &&
    ospite.numeroDocumento
  )
}

export function useOspite(strutturaId: string | null, prenotazioneId: string | null) {
  return useQuery({
    queryKey: ['ospiti', strutturaId, prenotazioneId],
    queryFn: async () => {
      try {
        return await apiGet<OspiteDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/ospiti`)
      } catch (err) {
        if (err instanceof ApiError && err.status === 404) return null
        throw err
      }
    },
    enabled: !!strutturaId && !!prenotazioneId,
  })
}

export function useSalvaSchedaOspiti(strutturaId: string | null, prenotazioneId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: SalvaSchedaOspitiRequest) =>
      apiPut<OspiteDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/ospiti`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ospiti', strutturaId, prenotazioneId] })
      queryClient.invalidateQueries({ queryKey: ['prenotazioni', strutturaId] })
    },
  })
}
