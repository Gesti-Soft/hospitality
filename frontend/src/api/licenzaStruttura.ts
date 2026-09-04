import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut } from './client'

// ---------------------------------------------------------------------------
// Licenza software GestiSoft di una Struttura (solo Super Admin) — NON la
// licenza Wubook (vedi api/superAdminImpostazioni.ts per quella): è la licenza
// che GestiSoft concede al Cliente per usare il gestionale, indipendente da
// quali integrazioni esterne siano concesse.
// ---------------------------------------------------------------------------

export interface LicenzaStrutturaDto {
  strutturaId: string
  scadenzaLicenza: string | null
}

export interface AggiornaLicenzaStrutturaRequest {
  scadenzaLicenza: string | null
  /** True solo se il Super Admin ha confermato che questa modifica alla scadenza è un rinnovo pagato dal Cliente — registra un incasso in Statistiche. */
  rinnovoPagato?: boolean
  importoRinnovo?: number | null
}

export function useLicenzaStruttura(strutturaId: string | null) {
  return useQuery({
    queryKey: ['licenza-struttura', strutturaId],
    queryFn: () => apiGet<LicenzaStrutturaDto>(`/strutture/${strutturaId}/licenza`),
    enabled: !!strutturaId,
  })
}

export function useAggiornaLicenzaStruttura(strutturaId: string | null) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: AggiornaLicenzaStrutturaRequest) => apiPut<LicenzaStrutturaDto>(`/strutture/${strutturaId}/licenza`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['licenza-struttura', strutturaId] })
      // La scadenza incide sulla Struttura mostrata nella Dashboard Super Admin e nel selettore
      // Struttura (etichetta "Da rinnovare"), entrambi alimentati da questi stessi dati aggregati.
      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
      queryClient.invalidateQueries({ queryKey: ['strutture'] })
    },
  })
}
