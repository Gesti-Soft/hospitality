import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface StatisticheKpiDto {
  numeroPrenotazioni: number
  ricavoStimato: number
  ricavoEffettivo: number
  permanenzaMediaNotti: number | null
  tassoOccupazionePercentuale: number
}

export interface VoceConteggioDto {
  etichetta: string
  conteggio: number
}

export interface VocePercentualeDto {
  etichetta: string
  conteggio: number
  percentuale: number
}

export interface ValoreMensileDto {
  mese: number
  valore: number
}

export interface VoceImportoDto {
  etichetta: string
  importo: number
}

export interface RigaMatriceMeseDto {
  mese: number
  conteggiPerTipologia: number[]
}

export interface MatricePrenotazioniTipologiaDto {
  tipologie: string[]
  righe: RigaMatriceMeseDto[]
}

export interface StatisticheTassaSoggiornoDto {
  totaleAnno: number
  andamentoMensile: ValoreMensileDto[]
}

export interface StatisticheStrutturaDto {
  anno: number
  kpi: StatisticheKpiDto
  prenotazioniPerAgenzia: VoceConteggioDto[]
  prenotazioniPerNazionalita: VocePercentualeDto[]
  andamentoRicavoMensile: ValoreMensileDto[]
  ricavoPerTipologiaCamera: VoceImportoDto[]
  prenotazioniPerTipologiaMese: MatricePrenotazioniTipologiaDto
  tassaSoggiorno: StatisticheTassaSoggiornoDto
}

export function useStatisticheStruttura(strutturaId: string | null, anno: number) {
  return useQuery({
    queryKey: ['statistiche', strutturaId, anno],
    queryFn: () => apiGet<StatisticheStrutturaDto>(`/strutture/${strutturaId}/statistiche?anno=${anno}`),
    enabled: !!strutturaId,
  })
}

/** Anni con almeno una prenotazione per questa struttura — per non proporre nel selettore anni sicuramente vuoti (es. l'anno prossimo). */
export function useAnniDisponibiliStatistiche(strutturaId: string | null) {
  return useQuery({
    queryKey: ['statistiche', strutturaId, 'anni'],
    queryFn: () => apiGet<number[]>(`/strutture/${strutturaId}/statistiche/anni`),
    enabled: !!strutturaId,
  })
}
