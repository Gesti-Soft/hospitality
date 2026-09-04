import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut } from './client'

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
