import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useSpese, useEliminaSpesa, type SpesaDto } from '../api/spese'
import { ApiError } from '../api/client'
import { fontMono, tokens } from '../theme'
import { SpesaDialog } from '../components/SpesaDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { ANNO_CORRENTE, AzioneNuovo, Cornice, formattatoreData, formattatoreValuta, IntestazioneFinanze, RigaVuota } from '../components/finanze/FinanzeComuni'

export function SpesePage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const [errore, setErrore] = useState<string | null>(null)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | SpesaDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<SpesaDto | null>(null)

  const spese = useSpese(strutturaId, anno)
  const elimina = useEliminaSpesa(strutturaId)

  function confermaEliminaSpesa() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, {
      onSuccess: () => setDaEliminare(null),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const dati = spese.data ?? []
  const totale = dati.reduce((acc, s) => acc + s.importoSpesa, 0)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze
        titolo="Spese"
        anno={anno}
        onAnnoChange={setAnno}
        azioni={<AzioneNuovo etichetta="+ Nuova spesa" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} />}
      />

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {spese.isLoading && <Skeleton variant="rounded" height={220} />}

      {!spese.isLoading && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Nome</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Metodo</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dati.length === 0 && <RigaVuota colSpan={6} messaggio="Nessuna spesa registrata per l'anno selezionato." />}
              {dati.map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{s.dataSpesa ? formattatoreData.format(new Date(s.dataSpesa)) : '—'}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{s.nome}</TableCell>
                  <TableCell>{s.tipoSpesa ?? '—'}</TableCell>
                  <TableCell>{s.metodoPagamento ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700, color: tokens.error600 }}>
                    {formattatoreValuta.format(s.importoSpesa)}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo(s)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => setDaEliminare(s)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {dati.length > 0 && (
                <TableRow>
                  <TableCell colSpan={4} />
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                    {formattatoreValuta.format(totale)}
                  </TableCell>
                  <TableCell />
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && strutturaId && <SpesaDialog strutturaId={strutturaId} spesa={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare spesa"
          messaggio={`Eliminare la spesa "${daEliminare.nome}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaSpesa}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
