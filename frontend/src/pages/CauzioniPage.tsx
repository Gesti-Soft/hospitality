import { useState } from 'react'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { useCauzioni } from '../api/finanze'
import { fontMono, tokens } from '../theme'
import { ANNO_CORRENTE, Cornice, formattatoreData, formattatoreValuta, IntestazioneFinanze, RigaVuota } from '../components/finanze/FinanzeComuni'

export function CauzioniPage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const cauzioni = useCauzioni(strutturaId, anno)
  const dati = cauzioni.data ?? []

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze titolo="Cauzioni" anno={anno} onAnnoChange={setAnno} />

      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Registrate automaticamente al check-out quando la cauzione non viene restituita per intero — nessuna registrazione manuale.
      </Typography>

      {cauzioni.isLoading && <Skeleton variant="rounded" height={180} />}

      {!cauzioni.isLoading && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell align="right">Importo trattenuto</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dati.length === 0 && <RigaVuota colSpan={2} messaggio="Nessuna cauzione trattenuta per l'anno selezionato." />}
              {dati.map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{c.dataInserimento ? formattatoreData.format(new Date(c.dataInserimento)) : '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                    {c.importoCauzione != null ? formattatoreValuta.format(c.importoCauzione) : '—'}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Cornice>
      )}
    </Box>
  )
}
