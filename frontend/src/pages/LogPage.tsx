import { useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { useStruttura } from '../struttura/StrutturaContext'
import { CATEGORIE_LOG, LivelloLog, useLogs, type LogEventoDto } from '../api/log'
import { fontMono, tokens } from '../theme'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' })
const PAGE_SIZE = 25

const ETICHETTA_LIVELLO: Record<LivelloLog, string> = {
  [LivelloLog.Info]: 'Info',
  [LivelloLog.Warning]: 'Avviso',
  [LivelloLog.Error]: 'Errore',
}

const COLORE_LIVELLO: Record<LivelloLog, string> = {
  [LivelloLog.Info]: tokens.blue600,
  [LivelloLog.Warning]: tokens.wait600,
  [LivelloLog.Error]: tokens.error600,
}

export function LogPage() {
  const { strutturaId } = useStruttura()
  const [livello, setLivello] = useState<string>('')
  const [categoria, setCategoria] = useState<string>('')
  const [page, setPage] = useState(1)
  const [dettaglio, setDettaglio] = useState<LogEventoDto | null>(null)

  const logs = useLogs(strutturaId, livello === '' ? null : (Number(livello) as LivelloLog), categoria === '' ? null : categoria, page, PAGE_SIZE)

  const totalPages = logs.data ? Math.max(1, Math.ceil(logs.data.totalCount / PAGE_SIZE)) : 1

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <Box sx={{ display: 'flex', gap: 1.5 }}>
          <TextField
            select
            size="small"
            label="Livello"
            value={livello}
            onChange={(e) => {
              setLivello(e.target.value)
              setPage(1)
            }}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value="">Tutti</MenuItem>
            <MenuItem value={String(LivelloLog.Info)}>Info</MenuItem>
            <MenuItem value={String(LivelloLog.Warning)}>Avviso</MenuItem>
            <MenuItem value={String(LivelloLog.Error)}>Errore</MenuItem>
          </TextField>

          <TextField
            select
            size="small"
            label="Categoria"
            value={categoria}
            onChange={(e) => {
              setCategoria(e.target.value)
              setPage(1)
            }}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">Tutte</MenuItem>
            {CATEGORIE_LOG.map((c) => (
              <MenuItem key={c} value={c}>
                {c}
              </MenuItem>
            ))}
          </TextField>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
            Pagina {page} di {totalPages} ({logs.data?.totalCount ?? 0} eventi)
          </Typography>
          <IconButton size="small" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}>
            <ChevronRightIcon fontSize="small" />
          </IconButton>
        </Box>
      </Box>

      {logs.isLoading && <Skeleton variant="rounded" height={320} />}

      {!logs.isLoading && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Livello</TableCell>
                <TableCell>Categoria</TableCell>
                <TableCell>Operatore</TableCell>
                <TableCell>Messaggio</TableCell>
                <TableCell>Correlation Id</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(logs.data?.items ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun evento registrato.
                  </TableCell>
                </TableRow>
              )}
              {(logs.data?.items ?? []).map((l) => (
                <TableRow key={l.id} hover onClick={() => setDettaglio(l)} sx={{ cursor: l.dettaglio ? 'pointer' : 'default' }}>
                  <TableCell sx={{ fontFamily: fontMono, fontSize: 12.5, whiteSpace: 'nowrap' }}>{formattatoreDataOra.format(new Date(l.createdAtUtc))}</TableCell>
                  <TableCell>
                    <Chip size="small" label={ETICHETTA_LIVELLO[l.livello]} sx={{ bgcolor: COLORE_LIVELLO[l.livello], color: '#fff', fontWeight: 700 }} />
                  </TableCell>
                  <TableCell sx={{ fontSize: 12.5 }}>{l.categoria ?? l.origine}</TableCell>
                  <TableCell sx={{ fontSize: 12.5, color: tokens.textSecondary }}>{l.operatore ?? '—'}</TableCell>
                  <TableCell sx={{ fontSize: 12.5 }}>{l.messaggio}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono, fontSize: 11, color: tokens.textTertiary }}>{l.correlationId ?? '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dettaglio && (
        <Dialog open onClose={() => setDettaglio(null)} maxWidth="md" fullWidth>
          <DialogTitle>Dettaglio evento</DialogTitle>
          <DialogContent>
            <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mb: 1.5 }}>{dettaglio.messaggio}</Typography>
            <Box
              component="pre"
              sx={{
                fontFamily: fontMono,
                fontSize: 12,
                bgcolor: tokens.paper,
                border: `1px solid ${tokens.surfaceBorder}`,
                borderRadius: 1.5,
                p: 2,
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
                maxHeight: 400,
                overflow: 'auto',
              }}
            >
              {dettaglio.dettaglio ?? 'Nessun dettaglio tecnico disponibile per questo evento.'}
            </Box>
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 2.5 }}>
            <Button onClick={() => setDettaglio(null)}>Chiudi</Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  )
}
