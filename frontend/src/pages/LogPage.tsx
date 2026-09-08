import { useCallback, useEffect, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Button from '@mui/material/Button'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import SearchIcon from '@mui/icons-material/Search'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../api/client'
import { CATEGORIE_LOG, CATEGORIE_LOG_CLIENTE, LivelloLog, useLogs, type LogEventoDto } from '../api/log'
import { fontMono, tokens } from '../theme'
import { useMobile } from '../lib/useMobile'
import { CardElenco, MessaggioVuotoElenco, RigaCardMeta, SentinellaCaricamentoElenco, TestataCardElenco } from '../components/CardElenco'

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
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const { sessione } = useAuth()
  const categorieDisponibili = sessione?.isSuperAdmin ? CATEGORIE_LOG : CATEGORIE_LOG_CLIENTE
  const [livello, setLivello] = useState<string>('')
  const [categoria, setCategoria] = useState<string>('')
  const [ricerca, setRicerca] = useState('')
  const [ricercaDebounced, setRicercaDebounced] = useState('')
  const [dettaglio, setDettaglio] = useState<LogEventoDto | null>(null)

  // Debounce: la ricerca è server-side (il log può contenere mesi di righe, non ha senso caricarle
  // tutte per filtrarle in memoria come nelle altre pagine) — senza attesa ogni tasto premuto
  // scatenerebbe una nuova query.
  useEffect(() => {
    const timeout = setTimeout(() => setRicercaDebounced(ricerca), 400)
    return () => clearTimeout(timeout)
  }, [ricerca])

  const logs = useLogs(strutturaId, livello === '' ? null : (Number(livello) as LivelloLog), categoria === '' ? null : categoria, ricercaDebounced, PAGE_SIZE)

  const eventi = logs.data?.pages.flatMap((p) => p.items) ?? []
  const totaleEventi = logs.data?.pages[0]?.totalCount ?? 0

  const elementoSentinella = useRef<HTMLElement | null>(null)
  const sentinellaRef = useCallback((el: HTMLElement | null) => {
    elementoSentinella.current = el
  }, [])
  useEffect(() => {
    if (!logs.hasNextPage) return
    const el = elementoSentinella.current
    if (!el) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && !logs.isFetchingNextPage) {
          logs.fetchNextPage()
        }
      },
      { rootMargin: '200px' },
    )
    observer.observe(el)
    return () => observer.disconnect()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [logs.hasNextPage, logs.isFetchingNextPage])

  const accessoNegato = logs.error instanceof ApiError && logs.error.status === 403

  if (accessoNegato) {
    return (
      <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 6, textAlign: 'center', color: tokens.textSecondary }}>
        Solo chi gestisce gli utenti di questa struttura può consultare il log.
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
          <TextField
            size="small"
            placeholder="Cerca per messaggio, operatore, correlation id o data (gg/mm)..."
            value={ricerca}
            onChange={(e) => setRicerca(e.target.value)}
            sx={{ minWidth: 280 }}
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

          <TextField
            select
            size="small"
            label="Livello"
            value={livello}
            onChange={(e) => setLivello(e.target.value)}
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
            onChange={(e) => setCategoria(e.target.value)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value="">Tutte</MenuItem>
            {categorieDisponibili.map((c) => (
              <MenuItem key={c} value={c}>
                {c}
              </MenuItem>
            ))}
          </TextField>
        </Box>

        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          {eventi.length} di {totaleEventi} eventi
        </Typography>
      </Box>

      {/* Valori duplicati da LogEventoService.GiorniConservazioneInfo/GiorniConservazioneAltri: tenere allineati. */}
      <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary }}>
        Per limitare la conservazione dei dati (art. 5.1.e GDPR), i log vengono eliminati automaticamente dopo 6 mesi (Info) o 12 mesi (Avviso/Errore).
      </Typography>

      {logs.isLoading && <Skeleton variant="rounded" height={320} />}

      {!logs.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {eventi.length === 0 && <MessaggioVuotoElenco messaggio="Nessun evento registrato." />}
          {eventi.map((l) => (
            <CardElenco key={l.id} onClick={l.dettaglio ? () => setDettaglio(l) : undefined}>
              <TestataCardElenco
                titolo={l.categoria ?? l.origine}
                sottotitolo={formattatoreDataOra.format(new Date(l.createdAtUtc))}
                azioneDestra={<Chip size="small" label={ETICHETTA_LIVELLO[l.livello]} sx={{ bgcolor: COLORE_LIVELLO[l.livello], color: '#fff', fontWeight: 700 }} />}
              />
              <Typography sx={{ fontSize: 12.5 }}>{l.messaggio}</Typography>
              <RigaCardMeta
                voci={[
                  { etichetta: 'Operatore', valore: l.operatore ?? '—' },
                  { etichetta: 'Correlation Id', valore: <Box component="span" sx={{ fontFamily: fontMono, fontSize: 11, color: tokens.textTertiary }}>{l.correlationId ?? '—'}</Box> },
                ]}
              />
            </CardElenco>
          ))}
          {logs.hasNextPage && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!logs.isLoading && !mobile && (
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
              {eventi.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun evento registrato.
                  </TableCell>
                </TableRow>
              )}
              {eventi.map((l) => (
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
              {logs.hasNextPage && (
                <TableRow ref={sentinellaRef}>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textTertiary, py: 2, fontSize: 12 }}>
                    Caricamento altri eventi...
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Box>
      )}

      {dettaglio && (
        <Dialog open onClose={() => setDettaglio(null)} maxWidth="md" fullWidth fullScreen={mobile}>
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
