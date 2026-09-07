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
import { fontDisplay, fontMono, tokens } from '../../theme'
import { CampoData } from '../CampoData'
import { inizioGiornoLocale, parsaInputData } from '../../lib/date'

export { ANNO_CORRENTE } from '../../lib/anni'

export const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
export const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

/**
 * Intestazione comune alle 4 pagine di Finanze: titolo a sinistra, selettore Anno (ed eventuali
 * azioni) a destra. `anni` è a carico di chi chiama (vedi `anniConAnnoCorrente`) — su richiesta
 * esplicita propone solo anni con dati reali più l'anno corrente, mai un range fisso arbitrario.
 */
export function IntestazioneFinanze({
  titolo,
  anno,
  anni,
  onAnnoChange,
  azioni,
}: {
  titolo: string
  anno: number
  anni: number[]
  onAnnoChange: (anno: number) => void
  azioni?: React.ReactNode
}) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 18 }}>{titolo}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {azioni}
        <TextField select size="small" label="Anno" value={anno} onChange={(e) => onAnnoChange(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {anni.map((a) => (
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

/**
 * Barra del totale sotto la tabella, fuori dalla `Cornice` e con `position: sticky` sul fondo
 * dell'area di contenuto scrollabile (vedi `overflow: 'auto'` in AppShell): con la paginazione a
 * scroll infinito la tabella può crescere molto in altezza, e un totale dentro l'ultima riga della
 * tabella sarebbe visibile solo scrollando fino in fondo a tutto l'elenco — qui invece resta sempre
 * a vista, indipendentemente da quante righe sono già state caricate.
 */
export function BarraTotale({ etichetta, valore, colore }: { etichetta: string; valore: number; colore?: string }) {
  return (
    <Box
      sx={{
        position: 'sticky',
        bottom: 0,
        zIndex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: 2,
        py: 1.25,
        bgcolor: tokens.surface,
        border: `1px solid ${tokens.surfaceBorder}`,
        borderRadius: 2,
        boxShadow: '0 -4px 10px rgba(0,0,0,0.06)',
      }}
    >
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: tokens.textSecondary }}>{etichetta}</Typography>
      <Typography sx={{ fontFamily: fontMono, fontWeight: 700, fontSize: 15, color: colore ?? tokens.textPrimary }}>
        {formattatoreValuta.format(valore)}
      </Typography>
    </Box>
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
