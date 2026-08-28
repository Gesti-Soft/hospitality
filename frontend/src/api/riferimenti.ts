import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface StatoDto {
  id: string
  codice: number
  descrizione: string
  nomeInglese: string | null
  acronimo: string | null
}

export interface ComuneDto {
  id: string
  codice: number
  descrizione: string
  provincia: string | null
  codiceBelfiore: string | null
  cap: string | null
}

export interface DocumentoDto {
  id: string
  codice: string
  descrizione: string
  typeId: number
}

export interface TipoAlloggiatoDto {
  id: string
  codice: string
  descrizione: string
}

/** Stati esteri (~236 righe): elenco completo, caricato una sola volta. */
export function useStati() {
  return useQuery({
    queryKey: ['riferimenti', 'stati'],
    queryFn: () => apiGet<StatoDto[]>('/riferimenti/stati'),
    staleTime: Infinity,
  })
}

/** Comuni italiani (~11.283 righe): ricerca lato server, non caricati tutti insieme. */
export function useComuni(ricerca: string) {
  return useQuery({
    queryKey: ['riferimenti', 'comuni', ricerca],
    queryFn: () => apiGet<ComuneDto[]>(`/riferimenti/comuni?ricerca=${encodeURIComponent(ricerca)}`),
    staleTime: 60_000,
  })
}

/** Tipi documento d'identità Alloggiati Web (95 righe): elenco completo, caricato una sola volta. */
export function useDocumenti() {
  return useQuery({
    queryKey: ['riferimenti', 'documenti'],
    queryFn: () => apiGet<DocumentoDto[]>('/riferimenti/documenti'),
    staleTime: Infinity,
  })
}

/** Classificazione ospite Alloggiati Web (5 righe): elenco completo, caricato una sola volta. */
export function useTipiAlloggiato() {
  return useQuery({
    queryKey: ['riferimenti', 'tipi-alloggiato'],
    queryFn: () => apiGet<TipoAlloggiatoDto[]>('/riferimenti/tipi-alloggiato'),
    staleTime: Infinity,
  })
}
