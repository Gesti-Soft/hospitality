import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import MenuItem from '@mui/material/MenuItem'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { fontDisplay, tokens } from '../../theme'

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

export function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}
