import { useEffect, useState } from 'react'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Popover from '@mui/material/Popover'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { aggiungiGiorni, differenzaGiorni, formatoInputData, parsaInputData } from '../lib/date'
import { tokens } from '../theme'

const GIORNI_SETTIMANA = ['lun', 'mar', 'mer', 'gio', 'ven', 'sab', 'dom']
const DIMENSIONE_BLOCCO_ANNI = 12
const formattatoreMese = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' })

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

interface Props {
  anchorEl: HTMLElement | null
  /** Formato "YYYY-MM-DD" o stringa vuota — giorno da evidenziare come selezionato. */
  valore: string
  onSeleziona: (valore: string) => void
  onClose: () => void
  /** Formato "YYYY-MM-DD" — giorni precedenti non selezionabili. */
  min?: string
  /** Formato "YYYY-MM-DD" — giorni successivi non selezionabili. */
  max?: string
  /** Mostra un pulsante "Oggi" che seleziona la data odierna — solo dove serve (es. il salto rapido
   * a un periodo nel Calendario prenotazioni), non nei campi data generici come CampoData. */
  mostraOggi?: boolean
}

/**
 * Calendario a popover (mese corrente + vista per anni a blocchi di 12) — estratto da `CampoData`
 * per essere riusato ovunque serva far scegliere una data col mouse senza un campo di testo
 * digitabile associato (es. il salto rapido a un periodo nel Calendario prenotazioni). Il
 * popover si apre/chiude in base ad `anchorEl` (stesso pattern di CampoData) e riparte sempre dal
 * mese di `valore` (o da oggi/dal min-max più vicino) ogni volta che viene riaperto.
 */
export function CalendarioPopover({ anchorEl, valore, onSeleziona, onClose, min, max, mostraOggi }: Props) {
  const [meseVisibile, setMeseVisibile] = useState<Date>(() => inizioMese(new Date()))
  const [vistaAnni, setVistaAnni] = useState(false)
  const [bloccoAnni, setBloccoAnni] = useState(() => inizioBloccoAnni(new Date().getFullYear()))

  const valoreData = valore ? parsaInputData(valore) : null
  const minData = min ? parsaInputData(min) : null
  const maxData = max ? parsaInputData(max) : null

  function fuoriLimiti(d: Date): boolean {
    return !!((minData && d < minData) || (maxData && d > maxData))
  }

  useEffect(() => {
    if (!anchorEl) return
    const base = valoreData && !fuoriLimiti(valoreData) ? valoreData : (minData ?? maxData ?? new Date())
    setMeseVisibile(inizioMese(base))
    setVistaAnni(false)
    // Riparte solo quando il popover si apre (transizione di anchorEl), non ad ogni cambio di
    // valore/min/max mentre resta aperto — altrimenti navigare tra i mesi verrebbe annullato appena
    // il chiamante ri-renderizza per un motivo indipendente.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [anchorEl])

  const griglia = costruisciGriglia(meseVisibile)
  const anniBlocco = Array.from({ length: DIMENSIONE_BLOCCO_ANNI }, (_, i) => bloccoAnni + i)

  return (
    <Popover
      open={!!anchorEl}
      anchorEl={anchorEl}
      onClose={onClose}
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
                    onClick={() => !fuoriRange && onSeleziona(formatoInputData(data))}
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
            {mostraOggi && (
              <Box sx={{ display: 'flex', justifyContent: 'center', mt: 1, pt: 1, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
                <Typography
                  component="button"
                  type="button"
                  onClick={() => onSeleziona(formatoInputData(new Date()))}
                  sx={{
                    fontSize: 12.5,
                    fontWeight: 700,
                    background: 'none',
                    border: 'none',
                    cursor: 'pointer',
                    borderRadius: 1,
                    px: 1,
                    py: 0.4,
                    color: tokens.blue600,
                    '&:hover': { bgcolor: tokens.paper },
                  }}
                >
                  Oggi
                </Typography>
              </Box>
            )}
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
  )
}
