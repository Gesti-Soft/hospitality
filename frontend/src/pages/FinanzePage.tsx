import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useCauzioni, useRiepilogoCassa } from '../api/finanze'
import { useSpese, useEliminaSpesa, type SpesaDto } from '../api/spese'
import { useEntrate, useEliminaEntrata, type EntrataDto } from '../api/entrate'
import { ApiError } from '../api/client'
import { fontDisplay, fontMono, tokens } from '../theme'
import { SpesaDialog } from '../components/SpesaDialog'
import { EntrataDialog } from '../components/EntrataDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'
import Alert from '@mui/material/Alert'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

const ANNO_CORRENTE = new Date().getFullYear()
const ANNI_DISPONIBILI = [ANNO_CORRENTE - 2, ANNO_CORRENTE - 1, ANNO_CORRENTE, ANNO_CORRENTE + 1]

type Tab_ = 'riepilogo' | 'spese' | 'entrate' | 'cauzioni'

export function FinanzePage() {
  const { strutturaId } = useStruttura()
  const [tab, setTab] = useState<Tab_>('riepilogo')
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const [errore, setErrore] = useState<string | null>(null)

  const riepilogo = useRiepilogoCassa(strutturaId, anno)
  const spese = useSpese(strutturaId, anno)
  const entrate = useEntrate(strutturaId, anno)
  const cauzioni = useCauzioni(strutturaId, anno)

  function segnalaErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ minHeight: 0 }}>
          <Tab label="Riepilogo" value="riepilogo" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Spese" value="spese" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Entrate" value="entrate" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Cauzioni" value="cauzioni" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        </Tabs>

        <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {ANNI_DISPONIBILI.map((a) => (
            <MenuItem key={a} value={a}>
              {a}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {tab === 'riepilogo' && <TabRiepilogo dati={riepilogo.data} caricamento={riepilogo.isLoading} />}
      {tab === 'spese' && <TabSpese strutturaId={strutturaId} spese={spese.data} caricamento={spese.isLoading} onErrore={segnalaErrore} />}
      {tab === 'entrate' && <TabEntrate strutturaId={strutturaId} entrate={entrate.data} caricamento={entrate.isLoading} onErrore={segnalaErrore} />}
      {tab === 'cauzioni' && <TabCauzioni cauzioni={cauzioni.data} caricamento={cauzioni.isLoading} />}
    </Box>
  )
}

function Cornice({ children }: { children: React.ReactNode }) {
  return <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>{children}</Box>
}

function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}

function TabRiepilogo({ dati, caricamento }: { dati: ReturnType<typeof useRiepilogoCassa>['data']; caricamento: boolean }) {
  if (caricamento) return <Skeleton variant="rounded" height={160} />
  if (!dati) return null

  const righe = [
    { etichetta: 'Incassi prenotazioni', valore: dati.importoPagatoPrenotazioni, segno: '+' as const },
    { etichetta: 'Cauzioni trattenute', valore: dati.cauzioni, segno: '+' as const },
    { etichetta: 'Entrate', valore: dati.entrate, segno: '+' as const },
    { etichetta: 'Spese', valore: dati.spese, segno: '-' as const },
  ]

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 2 }}>
      {righe.map((r) => (
        <Box key={r.etichetta} sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.textSecondary }}>{r.etichetta}</Typography>
          <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 600, mt: 0.75 }}>
            {r.segno === '-' ? '−' : ''}
            {formattatoreValuta.format(r.valore)}
          </Typography>
        </Box>
      ))}
      <Box sx={{ border: `1.5px solid ${tokens.orange600}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.orange700 }}>Saldo cassa</Typography>
        <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 700, mt: 0.75 }}>{formattatoreValuta.format(dati.saldo)}</Typography>
      </Box>
    </Box>
  )
}

function IntestazioneTab({ titolo, azione }: { titolo: string; azione: { etichetta: string; onClick: () => void; disabilitato?: boolean } }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>{titolo}</Typography>
      <Button variant="contained" color="secondary" size="small" onClick={azione.onClick} disabled={azione.disabilitato}>
        {azione.etichetta}
      </Button>
    </Box>
  )
}

function TabSpese({
  strutturaId,
  spese,
  caricamento,
  onErrore,
}: {
  strutturaId: string | null
  spese: SpesaDto[] | undefined
  caricamento: boolean
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | SpesaDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<SpesaDto | null>(null)
  const elimina = useEliminaSpesa(strutturaId)

  function confermaEliminaSpesa() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null), onError: onErrore })
  }

  const totale = (spese ?? []).reduce((acc, s) => acc + s.importoSpesa, 0)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Spese" azione={{ etichetta: '+ Nuova spesa', onClick: () => setDialogo('nuova'), disabilitato: !strutturaId }} />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && (
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
              {(spese ?? []).length === 0 && <RigaVuota colSpan={6} messaggio="Nessuna spesa registrata per l'anno selezionato." />}
              {(spese ?? []).map((s) => (
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
              {(spese ?? []).length > 0 && (
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

function TabEntrate({
  strutturaId,
  entrate,
  caricamento,
  onErrore,
}: {
  strutturaId: string | null
  entrate: EntrataDto[] | undefined
  caricamento: boolean
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | EntrataDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<EntrataDto | null>(null)
  const elimina = useEliminaEntrata(strutturaId)

  function confermaEliminaEntrata() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null), onError: onErrore })
  }

  const totale = (entrate ?? []).reduce((acc, e) => acc + e.importoEntrata, 0)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Entrate" azione={{ etichetta: '+ Nuova entrata', onClick: () => setDialogo('nuova'), disabilitato: !strutturaId }} />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Nome</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(entrate ?? []).length === 0 && <RigaVuota colSpan={5} messaggio="Nessuna entrata registrata per l'anno selezionato." />}
              {(entrate ?? []).map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{e.data ? formattatoreData.format(new Date(e.data)) : '—'}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{e.nome}</TableCell>
                  <TableCell>{e.tipoEntrata ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700, color: tokens.ok600 }}>
                    {formattatoreValuta.format(e.importoEntrata)}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo(e)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => setDaEliminare(e)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {(entrate ?? []).length > 0 && (
                <TableRow>
                  <TableCell colSpan={3} />
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

      {dialogo !== 'chiuso' && strutturaId && <EntrataDialog strutturaId={strutturaId} entrata={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare entrata"
          messaggio={`Eliminare l'entrata "${daEliminare.nome}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaEntrata}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}

function TabCauzioni({ cauzioni, caricamento }: { cauzioni: ReturnType<typeof useCauzioni>['data']; caricamento: boolean }) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Cauzioni</Typography>
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Registrate automaticamente al check-out quando la cauzione non viene restituita per intero — nessuna registrazione manuale.
      </Typography>

      {caricamento && <Skeleton variant="rounded" height={180} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell align="right">Importo trattenuto</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(cauzioni ?? []).length === 0 && <RigaVuota colSpan={2} messaggio="Nessuna cauzione trattenuta per l'anno selezionato." />}
              {(cauzioni ?? []).map((c) => (
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
