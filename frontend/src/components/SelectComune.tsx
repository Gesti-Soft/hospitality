import { useEffect, useMemo, useRef, useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import CircularProgress from '@mui/material/CircularProgress'
import TextField from '@mui/material/TextField'
import { type ComuneDto, useComuni, useStati } from '../api/riferimenti'

/** Ritarda l'aggiornamento di un valore, per non interrogare il server ad ogni tasto premuto. */
function useValoreConRitardo<T>(valore: T, ritardoMs: number): T {
  const [valoreRitardato, setValoreRitardato] = useState(valore)
  useEffect(() => {
    const timer = setTimeout(() => setValoreRitardato(valore), ritardoMs)
    return () => clearTimeout(timer)
  }, [valore, ritardoMs])
  return valoreRitardato
}

/**
 * Un'opzione dell'elenco: un comune italiano oppure uno stato estero. Gli stati prendono la stessa
 * forma dei comuni perché a valle (schedina, PayTourist, fatturazione) il luogo è una descrizione e
 * basta — provincia, CAP e codice Belfiore semplicemente non esistono per uno stato.
 */
type OpzioneLuogo = ComuneDto & { estero: boolean }

/** Quanti stati esteri mostrare al massimo: stesso tetto che il server applica ai comuni. */
const MassimiStati = 50

/**
 * Select con ricerca sui luoghi: comuni italiani (~11.283 righe, ricerca lato server) **e** stati
 * esteri (236 righe, caricate una volta sola e filtrate qui).
 *
 * Gli stati ci sono perché i campi che usano questo componente sono luoghi, non comuni: chi è nato
 * all'estero ha uno stato come luogo di nascita, chi risiede all'estero come residenza, e un
 * documento può essere rilasciato fuori dall'Italia. Il backend lo dà già per scontato — la
 * schedina cerca il codice di un luogo per descrizione tra comuni e stati concatenati
 * (IAnagraficaAlloggiatiWebRepository.ListLuoghiAsync) — ma qui i soli comuni rendevano quei casi
 * impossibili da compilare.
 *
 * Restano fuori di proposito i campi che sono solo stato (Cittadinanza, Stato di nascita): quelli
 * usano SelectRiferimento con il solo elenco degli stati.
 */
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
  const stati = useStati()

  // Stessa relevanza applicata dal server ai comuni (RiferimentiRepository.CercaComuniAsync): prima
  // chi ha il termine più vicino all'inizio, poi il nome più corto, infine l'ordine alfabetico.
  // A elenco vuoto non si aggiunge nulla: senza un termine digitato l'elenco mostra già i primi 50
  // comuni, e accodarci 50 stati sarebbe solo rumore.
  const opzioniStati = useMemo<OpzioneLuogo[]>(() => {
    const termine = ricerca.trim().toLowerCase()
    if (termine.length === 0) return []

    return (stati.data ?? [])
      .filter((s) => s.descrizione.toLowerCase().includes(termine))
      .sort((a, b) => {
        const posizione = a.descrizione.toLowerCase().indexOf(termine) - b.descrizione.toLowerCase().indexOf(termine)
        if (posizione !== 0) return posizione
        if (a.descrizione.length !== b.descrizione.length) return a.descrizione.length - b.descrizione.length
        return a.descrizione.localeCompare(b.descrizione)
      })
      .slice(0, MassimiStati)
      .map((s) => ({ id: s.id, codice: s.codice, descrizione: s.descrizione, provincia: null, codiceBelfiore: null, cap: null, estero: true }))
  }, [stati.data, ricerca])

  // I comuni restano in testa: sono il caso di gran lunga più frequente, e le opzioni devono essere
  // contigue per gruppo perché l'intestazione di `groupBy` non si ripeta.
  const opzioni = useMemo<OpzioneLuogo[]>(
    () => [...(comuni.data ?? []).map((c) => ({ ...c, estero: false })), ...opzioniStati],
    [comuni.data, opzioniStati],
  )

  // Il testo mostrato segue il valore anche quando a cambiarlo è il modulo e non chi digita (es. il
  // comune compilato in automatico per un cliente estero): `testo` viene inizializzato una volta
  // sola, quindi senza questo allineamento il campo resterebbe visivamente vuoto mentre il valore
  // sotto è cambiato. Scatta solo su un cambio vero del valore: durante la digitazione libera
  // `value` non si muove, e dopo una selezione dalla lista il testo è già quello giusto.
  const ultimoValore = useRef(value)
  useEffect(() => {
    if (value !== ultimoValore.current) {
      ultimoValore.current = value
      setTesto(value)
    }
  }, [value])

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

  const opzioneSelezionata: OpzioneLuogo | null =
    value ? { id: '', codice: 0, descrizione: value, provincia: provinciaSelezionata, codiceBelfiore: null, cap: null, estero: false } : null

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
      groupBy={(o) => (o.estero ? 'Stati esteri' : 'Comuni italiani')}
      getOptionLabel={(o) => (o.provincia ? `${o.descrizione} (${o.provincia})` : o.descrizione)}
      onChange={(_, v) => {
        onChange(v?.descrizione ?? '')
        setTesto(v?.descrizione ?? '')
        setProvinciaSelezionata(v?.provincia ?? null)
        onComuneSelezionato?.(v ?? null)
      }}
      noOptionsText={ricerca.trim().length < 2 ? 'Digita per cercare...' : 'Nessun comune o stato trovato'}
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
