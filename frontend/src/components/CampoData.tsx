import { useEffect, useState, type MouseEvent } from 'react'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import Popover from '@mui/material/Popover'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CalendarIcon from '@mui/icons-material/CalendarTodayOutlined'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { aggiungiGiorni, differenzaGiorni, formatoInputData, parsaDataItaliana, parsaInputData } from '../lib/date'
import { tokens } from '../theme'

const GIORNI_SETTIMANA = ['lun', 'mar', 'mer', 'gio', 'ven', 'sab', 'dom']
const DIMENSIONE_BLOCCO_ANNI = 12
const formattatoreMese = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' })
const formattatoreVisualizzato = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

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

  const valoreData = value ? parsaInputData(value) : null
  const minData = min ? parsaInputData(min) : null
  const maxData = max ? parsaInputData(max) : null
  const testoFormattato = valoreData ? formattatoreVisualizzato.format(valoreData) : ''

  // Il testo digitato è un buffer locale: segue `value` finché l'operatore non inizia a scrivere,
  // così un cambiamento esterno (es. il check-out ricalcolato quando cambia il check-in altrove nel
  // form) si riflette subito, ma senza sovrascrivere ciò che si sta digitando in quel momento.
  const [testo, setTesto] = useState(testoFormattato)
  const [inModifica, setInModifica] = useState(false)

  useEffect(() => {
    if (!inModifica) {
      setTesto(testoFormattato)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testoFormattato])

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

  function seleziona(giorno: Date) {
    onChange(formatoInputData(giorno))
    setTesto(formattatoreVisualizzato.format(giorno))
    chiudi()
  }

  function inizioModifica() {
    setInModifica(true)
  }

  function commettiTesto() {
    setInModifica(false)

    if (testo.trim() === '') {
      if (value !== '') onChange('')
      return
    }

    const parsata = parsaDataItaliana(testo)
    if (!parsata || fuoriLimiti(parsata)) {
      // Testo non valido o fuori dai limiti consentiti: torna all'ultimo valore valido invece di
      // lasciare a video una data che non verrebbe mai salvata.
      setTesto(testoFormattato)
      return
    }

    onChange(formatoInputData(parsata))
    setTesto(formattatoreVisualizzato.format(parsata))
  }

  const griglia = costruisciGriglia(meseVisibile)
  const anniBlocco = Array.from({ length: DIMENSIONE_BLOCCO_ANNI }, (_, i) => bloccoAnni + i)

  return (
    <>
      <TextField
        label={label}
        value={testo}
        onFocus={inizioModifica}
        onChange={(e) => {
          inizioModifica()
          setTesto(e.target.value)
        }}
        onBlur={commettiTesto}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur()
          }
        }}
        placeholder="gg/mm/aaaa"
        fullWidth={fullWidth}
        required={required}
        disabled={disabled}
        error={error}
        helperText={helperText}
        size={size}
        slotProps={{
          input: {
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
                {anniBlocco.map((anno) => {
                  const selezionato = anno === meseVisibile.getFullYear()
                  return (
                    <Box
                      key={anno}
                      onClick={() => {
                        setMeseVisibile((m) => new Date(anno, m.getMonth(), 1))
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
                      {anno}
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
