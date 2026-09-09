import { useEffect, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import Popover from '@mui/material/Popover'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CalendarIcon from '@mui/icons-material/CalendarTodayOutlined'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { aggiungiGiorni, differenzaGiorni, formatoInputData, parsaInputData } from '../lib/date'
import { tokens } from '../theme'

type Segmento = 'g' | 'm' | 'a'

const LUNGHEZZA: Record<Segmento, number> = { g: 2, m: 2, a: 4 }
const FILLER: Record<Segmento, string> = { g: 'g', m: 'm', a: 'a' }
const MASSIMO: Record<Segmento, number> = { g: 31, m: 12, a: 9999 }
const SUCCESSIVO: Record<Segmento, Segmento | null> = { g: 'm', m: 'a', a: null }
const ORDINE: Segmento[] = ['g', 'm', 'a']

/** Indici [inizio, fine) del segmento nel testo "gg/mm/aaaa" (i "/" sono in posizione 2 e 5). */
function rangeSegmento(s: Segmento): [number, number] {
  if (s === 'g') return [0, 2]
  if (s === 'm') return [3, 5]
  return [6, 10]
}

function conFiller(valore: string, s: Segmento): string {
  return (valore + FILLER[s].repeat(LUNGHEZZA[s])).slice(0, LUNGHEZZA[s])
}

/** Segmento corrispondente a una posizione del cursore nel testo "gg/mm/aaaa". */
function segmentoDaIndice(idx: number): Segmento {
  if (idx <= 2) return 'g'
  if (idx <= 5) return 'm'
  return 'a'
}

const GIORNI_SETTIMANA = ['lun', 'mar', 'mer', 'gio', 'ven', 'sab', 'dom']
const DIMENSIONE_BLOCCO_ANNI = 12
const formattatoreMese = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' })

interface Props {
  label: string
  /** Formato "YYYY-MM-DD" (come un &lt;input type="date"&gt;) o stringa vuota. */
  value: string
  onChange: (value: string) => void
  /** Formato "YYYY-MM-DD" — giorni precedenti non selezionabili. */
  min?: string
  /** Formato "YYYY-MM-DD" — giorni successivi non selezionabili. */
  max?: string
  fullWidth?: boolean
  required?: boolean
  disabled?: boolean
  error?: boolean
  helperText?: string
  size?: 'small' | 'medium'
}

function inizioMese(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1)
}

function aggiungiMesi(d: Date, n: number): Date {
  return new Date(d.getFullYear(), d.getMonth() + n, 1)
}

function inizioBloccoAnni(anno: number): number {
  return Math.floor(anno / DIMENSIONE_BLOCCO_ANNI) * DIMENSIONE_BLOCCO_ANNI
}

function costruisciGriglia(meseVisibile: Date): { data: Date; delMese: boolean }[] {
  // Griglia lunedì-domenica: offset del primo giorno del mese rispetto al lunedì.
  const offset = (meseVisibile.getDay() + 6) % 7
  const inizioGriglia = aggiungiGiorni(meseVisibile, -offset)
  return Array.from({ length: 42 }, (_, i) => {
    const data = aggiungiGiorni(inizioGriglia, i)
    return { data, delMese: data.getMonth() === meseVisibile.getMonth() }
  })
}

/**
 * Calendario personalizzato (niente picker nativo del browser, diverso su ogni sistema/browser e
 * non vincolabile a piacere): stesso value/onChange in formato stringa "YYYY-MM-DD" di un
 * &lt;input type="date"&gt;, così sostituisce ovunque quel pattern senza toccare la logica attorno.
 * Il campo è digitabile (gg/mm/aaaa) — indispensabile per una data lontana come una data di nascita,
 * dove cliccare mese per mese sarebbe impraticabile — e l'intestazione del calendario apre una vista
 * per anni (blocchi di 12) per la stessa ragione quando si preferisce comunque il mouse.
 */
export function CampoData({ label, value, onChange, min, max, fullWidth, required, disabled, error, helperText, size }: Props) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const [meseVisibile, setMeseVisibile] = useState<Date>(() => inizioMese(new Date()))
  const [vistaAnni, setVistaAnni] = useState(false)
  const [bloccoAnni, setBloccoAnni] = useState(() => inizioBloccoAnni(new Date().getFullYear()))
  const inputRef = useRef<HTMLInputElement>(null)

  const valoreData = value ? parsaInputData(value) : null
  const minData = min ? parsaInputData(min) : null
  const maxData = max ? parsaInputData(max) : null

  // Buffer dei 3 segmenti digitabili separatamente (giorno/mese/anno): seguono `value` finché
  // l'operatore non inizia a scrivere, così un cambiamento esterno (es. il check-out ricalcolato
  // quando cambia il check-in altrove nel form) si riflette subito, senza sovrascrivere ciò che si
  // sta digitando in quel momento.
  const [giorno, setGiorno] = useState('')
  const [mese, setMese] = useState('')
  const [anno, setAnno] = useState('')
  // Segmento con il focus e se il prossimo tasto digitato deve sovrascriverlo da zero (appena
  // selezionato, con clic o dopo l'avanzamento automatico) invece di accodarsi a quanto già scritto.
  const [segmento, setSegmento] = useState<Segmento>('g')
  const [fresh, setFresh] = useState(true)
  const [inModifica, setInModifica] = useState(false)

  useEffect(() => {
    if (inModifica) return
    if (value) {
      const [a, m, g] = value.split('-')
      setAnno(a)
      setMese(m)
      setGiorno(g)
    } else {
      setAnno('')
      setMese('')
      setGiorno('')
    }
  }, [value, inModifica])

  // Tiene evidenziato (selezionato) il segmento attivo nell'input reale, così l'operatore vede
  // sempre quale porzione della data sta per sovrascrivere digitando.
  useEffect(() => {
    const el = inputRef.current
    if (!el || document.activeElement !== el) return
    const [inizio, fine] = rangeSegmento(segmento)
    el.setSelectionRange(inizio, fine)
  }, [segmento, giorno, mese, anno])

  const testo = `${conFiller(giorno, 'g')}/${conFiller(mese, 'm')}/${conFiller(anno, 'a')}`

  function fuoriLimiti(d: Date): boolean {
    return !!((minData && d < minData) || (maxData && d > maxData))
  }

  function apriCalendario(e: MouseEvent<HTMLElement>) {
    if (disabled) return
    const base = valoreData && !fuoriLimiti(valoreData) ? valoreData : minData ?? maxData ?? new Date()
    setMeseVisibile(inizioMese(base))
    setVistaAnni(false)
    setAnchorEl(e.currentTarget)
  }

  function chiudi() {
    setAnchorEl(null)
    setVistaAnni(false)
  }

  function seleziona(giornoScelto: Date) {
    onChange(formatoInputData(giornoScelto))
    setInModifica(false)
    chiudi()
  }

  /** Focus da tastiera (es. Tab): seleziona il giorno, pronto per essere riscritto da capo. */
  function selezionaGiorno() {
    setInModifica(true)
    setSegmento('g')
    setFresh(true)
    inputRef.current?.setSelectionRange(...rangeSegmento('g'))
  }

  /** Clic sul campo: seleziona il segmento (giorno/mese/anno) su cui si è cliccato. */
  function selezionaSegmentoAlClick(e: MouseEvent<HTMLElement>) {
    const target = e.target as HTMLInputElement
    const s = segmentoDaIndice(target.selectionStart ?? 0)
    setInModifica(true)
    setSegmento(s)
    setFresh(true)
    inputRef.current?.setSelectionRange(...rangeSegmento(s))
  }

  function valoreSegmento(s: Segmento): string {
    return s === 'g' ? giorno : s === 'm' ? mese : anno
  }

  function impostaSegmento(s: Segmento, v: string) {
    if (s === 'g') setGiorno(v)
    else if (s === 'm') setMese(v)
    else setAnno(v)
  }

  function avanzaSegmento() {
    const successivo = SUCCESSIVO[segmento]
    if (successivo) {
      setSegmento(successivo)
      setFresh(true)
    }
  }

  function spostaSegmento(direzione: 1 | -1) {
    const idx = ORDINE.indexOf(segmento)
    const prossimo = ORDINE[Math.min(ORDINE.length - 1, Math.max(0, idx + direzione))]
    setSegmento(prossimo)
    setFresh(true)
  }

  function scriviCifra(cifra: string) {
    const lunghezza = LUNGHEZZA[segmento]
    const massimo = MASSIMO[segmento]

    let nuovo = fresh ? cifra : valoreSegmento(segmento) + cifra
    // Overflow di lunghezza (una terza cifra su un segmento già pieno) o di valore (es. giorno "39",
    // mese "13"): scarta quanto scritto finora e riparte da questa sola cifra, invece di restare
    // bloccati su un segmento ormai invalido.
    if (nuovo.length > lunghezza || Number(nuovo) > massimo) nuovo = cifra

    // Una singola cifra che da sola eccede già il massimo possibile a due cifre (es. giorno "4":
    // 40-49 > 31, mese "3": 30-39 > 12) determina subito il valore finale del segmento senza
    // aspettare una seconda cifra — stesso comportamento dei picker nativi (basta scrivere "4" per
    // il giorno 4, non serve "04").
    const completo = nuovo.length === lunghezza
    const decisoAlPrimoDigito = nuovo.length === 1 && Number(nuovo) * 10 > massimo
    if (decisoAlPrimoDigito) nuovo = nuovo.padStart(lunghezza, '0')

    impostaSegmento(segmento, nuovo)
    setFresh(false)
    setInModifica(true)

    if (completo || decisoAlPrimoDigito) avanzaSegmento()
  }

  function cancellaCifra() {
    setInModifica(true)
    const corrente = valoreSegmento(segmento)
    if (corrente.length > 0) {
      impostaSegmento(segmento, corrente.slice(0, -1))
      setFresh(false)
      return
    }
    const precedente = segmento === 'a' ? 'm' : segmento === 'm' ? 'g' : null
    if (!precedente) return
    setSegmento(precedente)
    impostaSegmento(precedente, valoreSegmento(precedente).slice(0, -1))
    setFresh(false)
  }

  function onKeyDownCampo(e: KeyboardEvent<HTMLElement>) {
    if (e.ctrlKey || e.metaKey || e.altKey || e.key === 'Tab') return
    if (e.key === 'Enter') {
      e.currentTarget.blur()
      return
    }
    if (e.key === 'Backspace') {
      e.preventDefault()
      cancellaCifra()
      return
    }
    if (e.key === 'ArrowLeft') {
      e.preventDefault()
      spostaSegmento(-1)
      return
    }
    if (e.key === 'ArrowRight') {
      e.preventDefault()
      spostaSegmento(1)
      return
    }
    if (/^[0-9]$/.test(e.key)) {
      e.preventDefault()
      scriviCifra(e.key)
      return
    }
    e.preventDefault()
  }

  function commetti() {
    setInModifica(false)

    if (!giorno && !mese && !anno) {
      if (value !== '') onChange('')
      return
    }

    const ripristina = () => {
      if (value) {
        const [a, m, g] = value.split('-')
        setAnno(a)
        setMese(m)
        setGiorno(g)
      } else {
        setAnno('')
        setMese('')
        setGiorno('')
      }
    }

    if (giorno.length < 2 || mese.length < 2 || anno.length < 4) {
      // Data incompleta: torna all'ultimo valore valido invece di lasciare a video un segmento vuoto.
      ripristina()
      return
    }

    const g = Number(giorno)
    const m = Number(mese)
    const a = Number(anno)
    const data = new Date(a, m - 1, g)
    const valida = data.getFullYear() === a && data.getMonth() === m - 1 && data.getDate() === g
    if (!valida || fuoriLimiti(data)) {
      ripristina()
      return
    }

    onChange(formatoInputData(data))
  }

  const griglia = costruisciGriglia(meseVisibile)
  const anniBlocco = Array.from({ length: DIMENSIONE_BLOCCO_ANNI }, (_, i) => bloccoAnni + i)

  return (
    <>
      <TextField
        label={label}
        value={testo}
        onFocus={selezionaGiorno}
        onClick={selezionaSegmentoAlClick}
        onChange={() => {
          /* Input pienamente controllato via onKeyDown/onPaste: nessuna modifica diretta da qui. */
        }}
        onPaste={(e) => e.preventDefault()}
        onBlur={commetti}
        onKeyDown={onKeyDownCampo}
        fullWidth={fullWidth}
        required={required}
        disabled={disabled}
        error={error}
        helperText={helperText}
        size={size}
        slotProps={{
          input: {
            inputRef,
            endAdornment: (
              <InputAdornment position="end">
                <IconButton size="small" onClick={apriCalendario} disabled={disabled} edge="end">
                  <CalendarIcon fontSize="small" sx={{ color: tokens.textTertiary }} />
                </IconButton>
              </InputAdornment>
            ),
          },
          // Sempre "ristretta" (come i vecchi <input type="date">) anche a campo vuoto: evita che
          // la label si sovrapponga al placeholder o "salti" quando si apre/chiude il calendario.
          inputLabel: { shrink: true },
        }}
      />
      <Popover
        open={!!anchorEl}
        anchorEl={anchorEl}
        onClose={chiudi}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      >
        <Box sx={{ p: 1.5, width: 268 }}>
          {!vistaAnni ? (
            <>
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
                <IconButton size="small" onClick={() => setMeseVisibile((m) => aggiungiMesi(m, -1))}>
                  <ChevronLeftIcon fontSize="small" />
                </IconButton>
                <Typography
                  component="button"
                  type="button"
                  onClick={() => {
                    setBloccoAnni(inizioBloccoAnni(meseVisibile.getFullYear()))
                    setVistaAnni(true)
                  }}
                  sx={{
                    fontSize: 13,
                    fontWeight: 700,
                    textTransform: 'capitalize',
                    background: 'none',
                    border: 'none',
                    cursor: 'pointer',
                    borderRadius: 1,
                    px: 0.75,
                    py: 0.25,
                    color: tokens.textPrimary,
                    '&:hover': { bgcolor: tokens.paper },
                  }}
                >
                  {formattatoreMese.format(meseVisibile)}
                </Typography>
                <IconButton size="small" onClick={() => setMeseVisibile((m) => aggiungiMesi(m, 1))}>
                  <ChevronRightIcon fontSize="small" />
                </IconButton>
              </Box>
              <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: '2px' }}>
                {GIORNI_SETTIMANA.map((g) => (
                  <Typography key={g} sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textAlign: 'center', py: 0.5 }}>
                    {g}
                  </Typography>
                ))}
                {griglia.map(({ data, delMese }) => {
                  const fuoriRange = fuoriLimiti(data)
                  const selezionato = valoreData ? differenzaGiorni(data, valoreData) === 0 : false
                  const oggi = differenzaGiorni(data, new Date()) === 0
                  return (
                    <Box
                      key={data.getTime()}
                      onClick={() => !fuoriRange && seleziona(data)}
                      sx={{
                        height: 30,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        borderRadius: '50%',
                        fontSize: 12.5,
                        fontWeight: selezionato ? 700 : 500,
                        cursor: fuoriRange ? 'default' : 'pointer',
                        color: fuoriRange ? tokens.textTertiary : selezionato ? '#fff' : tokens.textPrimary,
                        bgcolor: selezionato ? tokens.blue600 : 'transparent',
                        border: !selezionato && oggi ? `1px solid ${tokens.blue600}` : '1px solid transparent',
                        opacity: delMese ? (fuoriRange ? 0.4 : 1) : 0.3,
                        '&:hover': !fuoriRange ? { bgcolor: selezionato ? tokens.blue600 : tokens.paper } : undefined,
                      }}
                    >
                      {data.getDate()}
                    </Box>
                  )
                })}
              </Box>
            </>
          ) : (
            <>
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
                <IconButton size="small" onClick={() => setBloccoAnni((b) => b - DIMENSIONE_BLOCCO_ANNI)}>
                  <ChevronLeftIcon fontSize="small" />
                </IconButton>
                <Typography sx={{ fontSize: 13, fontWeight: 700 }}>
                  {bloccoAnni} – {bloccoAnni + DIMENSIONE_BLOCCO_ANNI - 1}
                </Typography>
                <IconButton size="small" onClick={() => setBloccoAnni((b) => b + DIMENSIONE_BLOCCO_ANNI)}>
                  <ChevronRightIcon fontSize="small" />
                </IconButton>
              </Box>
              <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '4px' }}>
                {anniBlocco.map((annoOpzione) => {
                  const selezionato = annoOpzione === meseVisibile.getFullYear()
                  return (
                    <Box
                      key={annoOpzione}
                      onClick={() => {
                        setMeseVisibile((m) => new Date(annoOpzione, m.getMonth(), 1))
                        setVistaAnni(false)
                      }}
                      sx={{
                        height: 34,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        borderRadius: 1,
                        fontSize: 12.5,
                        fontWeight: selezionato ? 700 : 500,
                        cursor: 'pointer',
                        color: selezionato ? '#fff' : tokens.textPrimary,
                        bgcolor: selezionato ? tokens.blue600 : 'transparent',
                        '&:hover': { bgcolor: selezionato ? tokens.blue600 : tokens.paper },
                      }}
                    >
                      {annoOpzione}
                    </Box>
                  )
                })}
              </Box>
            </>
          )}
        </Box>
      </Popover>
    </>
  )
}
