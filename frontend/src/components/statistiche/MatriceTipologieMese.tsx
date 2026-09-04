import Box from '@mui/material/Box'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { fontMono, tokens } from '../../theme'
import type { MatricePrenotazioniTipologiaDto } from '../../api/statistiche'

const NOMI_MESI = ['Gen', 'Feb', 'Mar', 'Apr', 'Mag', 'Giu', 'Lug', 'Ago', 'Set', 'Ott', 'Nov', 'Dic']

/**
 * Fallback in tabella per "prenotazioni per tipologia camera / mese" quando le tipologie sono
 * troppe per un grafico a barre multi-serie leggibile — a differenza dei grafici, mostra sempre
 * TUTTE le tipologie della struttura (il backend non applica Top7+Altro a questa matrice).
 */
export function MatriceTipologieMese({ matrice }: { matrice: MatricePrenotazioniTipologiaDto }) {
  if (matrice.tipologie.length === 0) {
    return <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna tipologia camera configurata.</Typography>
  }

  return (
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Mese</TableCell>
            {matrice.tipologie.map((t) => (
              <TableCell key={t} align="right">
                {t}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {matrice.righe.map((riga) => (
            <TableRow key={riga.mese} hover>
              <TableCell sx={{ fontWeight: 600 }}>{NOMI_MESI[riga.mese - 1]}</TableCell>
              {riga.conteggiPerTipologia.map((conteggio, i) => (
                <TableCell key={matrice.tipologie[i]} align="right" sx={{ fontFamily: fontMono }}>
                  {conteggio > 0 ? conteggio : '—'}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  )
}

export { NOMI_MESI }
