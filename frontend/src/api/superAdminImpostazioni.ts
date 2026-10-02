import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from './client'

// ---------------------------------------------------------------------------
// Impostazioni globali (a livello di applicazione, non di Cliente/Struttura)
// ---------------------------------------------------------------------------

export interface ImpostazioniGlobaliDto {
  idSoftwarePaytourist: number | null
  /** Uguale per tutte le Strutture (unico account partner Wubook) — non è un dato per Struttura. */
  tokenWubook: string | null
}

export function useImpostazioniGlobali(abilitato: boolean) {
  return useQuery({
    queryKey: ['impostazioni-globali'],
    queryFn: () => apiGet<ImpostazioniGlobaliDto>('/super-admin/impostazioni-globali'),
    enabled: abilitato,
  })
}

export function useAggiornaImpostazioniGlobali() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: ImpostazioniGlobaliDto) => apiPut<ImpostazioniGlobaliDto>('/super-admin/impostazioni-globali', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['impostazioni-globali'] }),
  })
}

// ---------------------------------------------------------------------------
// Credenziali Wubook per Struttura (codice struttura/lcode + utente-token gestisoft.it per il
// polling eventi) — solo Super Admin. NON la licenza software GestiSoft (scadenza): quella è per
// tutta la Struttura, indipendente da Wubook — vedi api/licenzaStruttura.ts.
// ---------------------------------------------------------------------------

export interface WubookLicenzaDto {
  strutturaId: string
  gestisoftUsername: string | null
  gestisoftToken: string | null
  codiceStruttura: string | null
}

export interface AggiornaWubookLicenzaRequest {
  gestisoftUsername: string | null
  gestisoftToken: string | null
  codiceStruttura: string | null
}

export function useWubookLicenzaSuperAdmin(strutturaId: string | null) {
  return useQuery({
    queryKey: ['wubook-licenza-super-admin', strutturaId],
    queryFn: () => apiGet<WubookLicenzaDto>(`/strutture/${strutturaId}/wubook/licenza`),
    enabled: !!strutturaId,
  })
}

export function useAggiornaWubookLicenzaSuperAdmin(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: AggiornaWubookLicenzaRequest) => apiPut<WubookLicenzaDto>(`/strutture/${strutturaId}/wubook/licenza`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['wubook-licenza-super-admin', strutturaId] })
      // Il codice struttura incide sul pallino "Wubook/licenza" mostrato nella Dashboard Super
      // Admin, che legge dagli stessi dati aggregati della dashboard.
      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
    },
  })
}

// ---------------------------------------------------------------------------
// Ricezione diretta delle prenotazioni OTA (l'OTA avvisa il gestionale invece di gestisoft.it) —
// solo Super Admin, vedi WubookAvvisiDirettiService lato backend.
// ---------------------------------------------------------------------------

export interface WubookAvvisiDirettiDto {
  attivi: boolean
  /** L'OTA ha registrato proprio l'indirizzo di questo gestionale. */
  indirizzoCorretto: boolean
  /** Indirizzo che l'OTA dice di avere ("questo gestionale" se è il nostro). */
  urlRegistrato: string | null
  urlPrecedente: string | null
  erroreLettura: string | null
  /** Valorizzato = da qui non si può attivare né disattivare (es. ambiente di sviluppo). */
  motivoNonAttivabile: string | null
}

export function useWubookAvvisiDiretti(strutturaId: string | null) {
  return useQuery({
    queryKey: ['wubook-avvisi-diretti', strutturaId],
    queryFn: () => apiGet<WubookAvvisiDirettiDto>(`/strutture/${strutturaId}/wubook/avvisi-diretti`),
    enabled: !!strutturaId,
  })
}

export function useCambiaWubookAvvisiDiretti(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attiva: boolean) =>
      apiPost<WubookAvvisiDirettiDto>(`/strutture/${strutturaId}/wubook/avvisi-diretti/${attiva ? 'attiva' : 'disattiva'}`),
    onSuccess: (dati) => queryClient.setQueryData(['wubook-avvisi-diretti', strutturaId], dati),
  })
}
