import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

// Stesso pattern di StatoPrenotazione in api/prenotazioni.ts: l'enum backend arriva come intero,
// qui rispecchiato con gli stessi valori invece di un union type stringa.
export const EsitoIntegrazione = { NonConcesso: 1, NonConfigurato: 2, Attesa: 3, Errore: 4, Attivo: 5 } as const
export type EsitoIntegrazione = (typeof EsitoIntegrazione)[keyof typeof EsitoIntegrazione]

export interface PanoramicaBusinessDto {
  clientiAttivi: number
  clientiTotali: number
  struttureAttive: number
  struttureTotali: number
}

export interface TrendMensileDto {
  mese: number
  conteggio: number
}

export interface IncassoRinnovoMensileDto {
  mese: number
  importo: number
}

export interface LicenzaScadutaDto {
  strutturaId: string
  nomeStruttura: string
  ragioneSocialeCliente: string
  scadenza: string | null
}

export interface LicenzaInScadenzaDto {
  strutturaId: string
  nomeStruttura: string
  ragioneSocialeCliente: string
  scadenza: string
  giorniRimanenti: number
}

export interface EsitoIntegrazioneDto {
  stato: EsitoIntegrazione
  ultimoInvioAtUtc: string | null
  ultimoErrore: string | null
}

export interface SaluteIntegrazioneStrutturaDto {
  strutturaId: string
  nomeStruttura: string
  ragioneSocialeCliente: string
  alloggiatiWeb: EsitoIntegrazioneDto
  osservatorio: EsitoIntegrazioneDto
  payTourist: EsitoIntegrazioneDto
  wubook: EsitoIntegrazioneDto
}

export interface StatisticheSuperAdminDto {
  panoramica: PanoramicaBusinessDto
  nuoviClientiPerMese: TrendMensileDto[]
  incassiRinnoviPerMese: IncassoRinnovoMensileDto[]
  licenzeScadute: LicenzaScadutaDto[]
  licenzeInScadenza: LicenzaInScadenzaDto[]
  saluteIntegrazioni: SaluteIntegrazioneStrutturaDto[]
}

export function useStatisticheSuperAdmin(abilitato: boolean, anno: number) {
  return useQuery({
    queryKey: ['super-admin', 'statistiche', anno],
    queryFn: () => apiGet<StatisticheSuperAdminDto>(`/super-admin/statistiche?anno=${anno}`),
    enabled: abilitato,
  })
}

/** Anni con almeno una prenotazione su una qualunque struttura — per non proporre nel selettore anni sicuramente vuoti. */
export function useAnniDisponibiliStatisticheSuperAdmin(abilitato: boolean) {
  return useQuery({
    queryKey: ['super-admin', 'statistiche', 'anni'],
    queryFn: () => apiGet<number[]>('/super-admin/statistiche/anni'),
    enabled: abilitato,
  })
}
