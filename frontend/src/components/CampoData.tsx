import { useState, type MouseEvent } from 'react'
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

const GIORNI_SETTIMANA = ['lun', 'mar', 'mer', 'gio', 'ven', 'sab', 'dom']
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
 */
export function CampoData({ label, value, onChange, min, max, fullWidth, required, disabled, error, helperText, size }: Props) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const [meseVisibile, setMeseVisibile] = useState<Date>(() => inizioMese(new Date()))

  const valoreData = value ? parsaInputData(value) : null
  const minData = min ? parsaInputData(min) : null
  const maxData = max ? parsaInputData(max) : null

  function apri(e: MouseEvent<HTMLElement>) {
    if (disabled) return
    // Se il valore attuale non è (più) valido rispetto a min/max — es. il check-out è rimasto
    // su un mese vecchio dopo aver spostato il check-in in avanti — il calendario si apre sul mese
    // del vincolo, non su un mese ormai fuori portata.
    const fuoriRange = valoreData && ((minData && valoreData < minData) || (maxData && valoreData > maxData))
    const base = valoreData && !fuoriRange ? valoreData : minData ?? maxData ?? new Date()
    setMeseVisibile(inizioMese(base))
    setAnchorEl(e.currentTarget)
  }

  function chiudi() {
    setAnchorEl(null)
  }

  function seleziona(giorno: Date) {
    onChange(formatoInputData(giorno))
    chiudi()
  }

  const griglia = costruisciGriglia(meseVisibile)

  return (
    <>
      <TextField
        label={label}
        value={valoreData ? formattatoreVisualizzato.format(valoreData) : ''}
        onClick={apri}
        fullWidth={fullWidth}
        required={required}
        disabled={disabled}
        error={error}
        helperText={helperText}
        size={size}
        slotProps={{
          input: {
            readOnly: true,
            endAdornment: (
              <InputAdornment position="end">
                <CalendarIcon fontSize="small" sx={{ color: tokens.textTertiary }} />
              </InputAdornment>
            ),
          },
          // Sempre "ristretta" (come i vecchi <input type="date">) anche a campo vuoto: evita che
          // la label si sovrapponga al placeholder o "salti" quando si apre/chiude il calendario.
          inputLabel: { shrink: true },
        }}
        sx={{ '& .MuiInputBase-input': { cursor: disabled ? 'default' : 'pointer' } }}
      />
      <Popover
        open={!!anchorEl}
        anchorEl={anchorEl}
        onClose={chiudi}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
      >
        <Box sx={{ p: 1.5, width: 268 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 0.5 }}>
            <IconButton size="small" onClick={() => setMeseVisibile((m) => aggiungiMesi(m, -1))}>
              <ChevronLeftIcon fontSize="small" />
            </IconButton>
            <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: 'capitalize' }}>{formattatoreMese.format(meseVisibile)}</Typography>
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
              const fuoriRange = (minData && data < minData) || (maxData && data > maxData)
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
        </Box>
      </Popover>
    </>
  )
}
