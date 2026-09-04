import { forwardRef } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import SearchIcon from '@mui/icons-material/Search'
import { fontDisplay, tokens } from '../../theme'
import { CampoData } from '../CampoData'
import { inizioGiornoLocale, parsaInputData } from '../../lib/date'

export const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
export const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

export const ANNO_CORRENTE = new Date().getFullYear()
export const ANNI_DISPONIBILI = [ANNO_CORRENTE - 2, ANNO_CORRENTE - 1, ANNO_CORRENTE, ANNO_CORRENTE + 1]

/** Intestazione comune alle 4 pagine di Finanze: titolo a sinistra, selettore Anno (ed eventuali azioni) a destra. */
export function IntestazioneFinanze({ titolo, anno, onAnnoChange, azioni }: { titolo: string; anno: number; onAnnoChange: (anno: number) => void; azioni?: React.ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 18 }}>{titolo}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {azioni}
        <TextField select size="small" label="Anno" value={anno} onChange={(e) => onAnnoChange(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {ANNI_DISPONIBILI.map((a) => (
            <MenuItem key={a} value={a}>
              {a}
            </MenuItem>
          ))}
        </TextField>
      </Box>
    </Box>
  )
}

export function AzioneNuovo({ etichetta, onClick, disabilitato }: { etichetta: string; onClick: () => void; disabilitato?: boolean }) {
  return (
    <Button variant="contained" color="primary" size="small" onClick={onClick} disabled={disabilitato}>
      {etichetta}
    </Button>
  )
}

export function Cornice({ children }: { children: React.ReactNode }) {
  return <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>{children}</Box>
}

/**
 * Riga di filtri comune alle pagine di Finanze: casella di ricerca testuale sempre presente, più un
 * intervallo di date "Da"/"A" opzionale (solo dove ha senso — es. non sulla rubrica Clienti
 * fatturabili, che non ha una data propria). Il filtro vero e proprio resta a carico di ogni pagina
 * (campi di ricerca ed eventuale campo data diversi caso per caso): qui c'è solo la UI condivisa.
 */
export function FiltriRicercaData({
  ricerca,
  onRicercaChange,
  placeholderRicerca,
  dataDa,
  onDataDaChange,
  dataA,
  onDataAChange,
}: {
  ricerca: string
  onRicercaChange: (valore: string) => void
  placeholderRicerca: string
  dataDa?: string
  onDataDaChange?: (valore: string) => void
  dataA?: string
  onDataAChange?: (valore: string) => void
}) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      <TextField
        size="small"
        placeholder={placeholderRicerca}
        value={ricerca}
        onChange={(e) => onRicercaChange(e.target.value)}
        sx={{ minWidth: 260, flex: '1 1 260px' }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: tokens.textTertiary }} />
              </InputAdornment>
            ),
          },
        }}
      />
      {onDataDaChange && onDataAChange && (
        <>
          <CampoData label="Da" value={dataDa ?? ''} onChange={onDataDaChange} size="small" max={dataA || undefined} />
          <CampoData label="A" value={dataA ?? ''} onChange={onDataAChange} size="small" min={dataDa || undefined} />
        </>
      )}
    </Box>
  )
}

/**
 * True se `valore` (data ISO del record, es. dataSpesa) cade nell'intervallo "Da"/"A" (formato
 * "YYYY-MM-DD" di CampoData, entrambi opzionali). Un record senza data propria non corrisponde a
 * nessun intervallo impostato — non ha senso includerlo "per difetto" in un filtro per data.
 */
export function nelRangeData(valore: string | null | undefined, da: string, a: string): boolean {
  if (!da && !a) return true
  if (!valore) return false

  const giorno = inizioGiornoLocale(new Date(valore))
  if (da && giorno < parsaInputData(da)) return false
  if (a && giorno > parsaInputData(a)) return false
  return true
}

export function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}

/** Riga "sentinella" per la paginazione a scroll infinito (vedi usePaginazioneScroll) — il ref va sull'elemento che l'IntersectionObserver osserva. */
export const RigaCaricamentoAltri = forwardRef<HTMLTableRowElement, { colSpan: number }>(function RigaCaricamentoAltri({ colSpan }, ref) {
  return (
    <TableRow ref={ref}>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textTertiary, py: 2, fontSize: 12 }}>
        Caricamento altri...
      </TableCell>
    </TableRow>
  )
})
