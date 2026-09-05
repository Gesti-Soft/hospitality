import { useEffect, useRef, useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import CircularProgress from '@mui/material/CircularProgress'
import TextField from '@mui/material/TextField'
import { type ComuneDto, useComuni } from '../api/riferimenti'

/** Ritarda l'aggiornamento di un valore, per non interrogare il server ad ogni tasto premuto. */
function useValoreConRitardo<T>(valore: T, ritardoMs: number): T {
  const [valoreRitardato, setValoreRitardato] = useState(valore)
  useEffect(() => {
    const timer = setTimeout(() => setValoreRitardato(valore), ritardoMs)
    return () => clearTimeout(timer)
  }, [valore, ritardoMs])
  return valoreRitardato
}

/** Select con ricerca lato server sui comuni italiani (~11.283 righe, non caricati tutti insieme). */
export function SelectComune({
  label,
  value,
  onChange,
  onComuneSelezionato,
  disabled,
  size,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  /** Chiamato con il comune completo (provincia/CAP/Belfiore inclusi) solo quando l'operatore ne sceglie uno dalla lista — non alla digitazione libera. */
  onComuneSelezionato?: (comune: ComuneDto | null) => void
  disabled?: boolean
  size?: 'small' | 'medium'
}) {
  const [testo, setTesto] = useState(value)
  // Provincia del comune scelto l'ultima volta (per disambiguare in etichetta gli omonimi, es.
  // "CASTELLAMMARE DEL GOLFO (TP)") — tenuta a parte invece di essere ri-derivata cercandola negli
  // ultimi risultati di ricerca: dopo una selezione il testo digitato include già "(TP)", quindi
  // ricercarlo per intero non troverebbe più nulla (il campo Descrizione non contiene la provincia)
  // e si perderebbe/ritroverebbe la provincia ad ogni giro di ricerca, con un fastidioso lampeggio.
  const [provinciaSelezionata, setProvinciaSelezionata] = useState<string | null>(null)
  const ricerca = useValoreConRitardo(testo, 300)
  const comuni = useComuni(ricerca)
  const opzioni = comuni.data ?? []

  // Risolve in automatico provincia/CAP/Belfiore di un valore già presente al montaggio (es. comune
  // di nascita copiato dalla scheda ospiti in un Cliente appena creato) non appena la ricerca
  // restituisce un riscontro esatto — altrimenti resterebbero noti solo se l'operatore riseleziona
  // a mano lo stesso comune già scritto in questo campo.
  const risoltoPer = useRef<string | null>(null)
  useEffect(() => {
    if (!value || risoltoPer.current === value) return
    const trovato = opzioni.find((o) => o.descrizione === value)
    if (trovato) {
      risoltoPer.current = value
      setProvinciaSelezionata(trovato.provincia)
      onComuneSelezionato?.(trovato)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, opzioni])

  const opzioneSelezionata: ComuneDto | null =
    value ? { id: '', codice: 0, descrizione: value, provincia: provinciaSelezionata, codiceBelfiore: null, cap: null } : null

  return (
    <Autocomplete
      fullWidth
      size={size}
      disabled={disabled}
      loading={comuni.isFetching}
      options={opzioni}
      filterOptions={(x) => x}
      value={opzioneSelezionata}
      inputValue={testo}
      onInputChange={(_, v, reason) => {
        if (reason === 'input') {
          setTesto(v)
          setProvinciaSelezionata(null)
          return
        }
        if (reason === 'clear') {
          setTesto('')
          return
        }
        // reason === 'reset': ignora il testo che MUI proporrebbe qui (l'etichetta "NOME (PR)",
        // ricalcolata anche solo perché `provinciaSelezionata` è cambiata, es. dalla risoluzione
        // automatica sopra) — se finisse in `testo` la ricerca ripartirebbe da quell'etichetta, che
        // non trova più nulla (la colonna Descrizione non contiene la provincia). Una vera selezione
        // dell'operatore è già gestita correttamente da onChange qui sotto.
      }}
      isOptionEqualToValue={(o, v) => o.descrizione === v.descrizione}
      getOptionLabel={(o) => (o.provincia ? `${o.descrizione} (${o.provincia})` : o.descrizione)}
      onChange={(_, v) => {
        onChange(v?.descrizione ?? '')
        setTesto(v?.descrizione ?? '')
        setProvinciaSelezionata(v?.provincia ?? null)
        onComuneSelezionato?.(v ?? null)
      }}
      noOptionsText={ricerca.trim().length < 2 ? 'Digita per cercare...' : 'Nessun comune trovato'}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          slotProps={{
            ...params.slotProps,
            input: {
              ...params.slotProps.input,
              endAdornment: (
                <>
                  {comuni.isFetching && <CircularProgress color="inherit" size={16} />}
                  {params.slotProps.input.endAdornment}
                </>
              ),
            },
          }}
        />
      )}
    />
  )
}
