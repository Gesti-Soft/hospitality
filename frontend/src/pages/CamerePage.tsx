import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
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
import { StatoCamera, useCamere, useEliminaCamera, type CameraDto } from '../api/camere'
import { useEliminaTipologia, useTipologie, type TipologiaCameraDto } from '../api/tipologie'
import { usePrezzi, useEliminaPrezzo, type PrezzoCameraDto } from '../api/prezzi'
import { useCanaliVendita, useCreaCanaleVendita, useAggiornaCanaleVendita, useEliminaCanaleVendita, type CanaleVenditaDto } from '../api/canaliVendita'
import { ApiError } from '../api/client'
import { fontDisplay, fontMono, tokens } from '../theme'
import { CameraDialog } from '../components/CameraDialog'
import { TipologiaDialog } from '../components/TipologiaDialog'
import { PrezzoDialog } from '../components/PrezzoDialog'

const ETICHETTA_STATO_CAMERA: Record<StatoCamera, string> = {
  [StatoCamera.Pronta]: 'Pronta',
  [StatoCamera.Occupata]: 'Occupata',
  [StatoCamera.DaPulire]: 'Da pulire',
  [StatoCamera.NonDisponibile]: 'Non disponibile',
}

const COLORE_STATO_CAMERA: Record<StatoCamera, string> = {
  [StatoCamera.Pronta]: tokens.ok600,
  [StatoCamera.Occupata]: tokens.blue600,
  [StatoCamera.DaPulire]: tokens.wait600,
  [StatoCamera.NonDisponibile]: tokens.error600,
}

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

type Tab_ = 'camere' | 'tipologie' | 'prezzi' | 'canali'

export function CamerePage() {
  const { strutturaId } = useStruttura()
  const [tab, setTab] = useState<Tab_>('camere')
  const [errore, setErrore] = useState<string | null>(null)

  const camere = useCamere(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const prezzi = usePrezzi(strutturaId)
  const canali = useCanaliVendita(strutturaId)

  function segnalaErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ minHeight: 0 }}>
          <Tab label="Camere" value="camere" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Tipologie" value="tipologie" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Prezzi" value="prezzi" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Canali vendita" value="canali" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        </Tabs>
      </Box>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {tab === 'camere' && (
        <TabCamere strutturaId={strutturaId} camere={camere.data} caricamento={camere.isLoading} tipologie={tipologie.data ?? []} onErrore={segnalaErrore} />
      )}
      {tab === 'tipologie' && <TabTipologie strutturaId={strutturaId} tipologie={tipologie.data} caricamento={tipologie.isLoading} onErrore={segnalaErrore} />}
      {tab === 'prezzi' && (
        <TabPrezzi
          strutturaId={strutturaId}
          prezzi={prezzi.data}
          caricamento={prezzi.isLoading}
          camere={camere.data ?? []}
          tipologie={tipologie.data ?? []}
          onErrore={segnalaErrore}
        />
      )}
      {tab === 'canali' && <TabCanali strutturaId={strutturaId} canali={canali.data} caricamento={canali.isLoading} onErrore={segnalaErrore} />}
    </Box>
  )
}

function Cornice({ children }: { children: React.ReactNode }) {
  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>{children}</Box>
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

function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}

// ---------------------------------------------------------------------------
// Camere
// ---------------------------------------------------------------------------

function TabCamere({
  strutturaId,
  camere,
  caricamento,
  tipologie,
  onErrore,
}: {
  strutturaId: string | null
  camere: CameraDto[] | undefined
  caricamento: boolean
  tipologie: TipologiaCameraDto[]
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | CameraDto>('chiuso')
  const elimina = useEliminaCamera(strutturaId)

  function eliminaCamera(camera: CameraDto) {
    if (!window.confirm(`Eliminare la camera "${camera.nome}"?`)) return
    elimina.mutate(camera.id, { onError: onErrore })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Camere" azione={{ etichetta: '+ Nuova camera', onClick: () => setDialogo('nuova'), disabilitato: !strutturaId }} />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell>Tipologia</TableCell>
                <TableCell>Stato</TableCell>
                <TableCell align="right">Capacità</TableCell>
                <TableCell align="right">Soggiorno minimo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(camere ?? []).length === 0 && <RigaVuota colSpan={6} messaggio="Nessuna camera configurata." />}
              {(camere ?? []).map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{c.nome}</TableCell>
                  <TableCell>{c.tipologiaNome ?? '—'}</TableCell>
                  <TableCell>
                    <Chip size="small" label={ETICHETTA_STATO_CAMERA[c.stateRoom]} sx={{ bgcolor: COLORE_STATO_CAMERA[c.stateRoom], color: '#fff', fontWeight: 700 }} />
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {c.capacitaOspiti ?? '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {c.soggiornoMinimo ?? '—'}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo(c)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => eliminaCamera(c)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <CameraDialog
          strutturaId={strutturaId}
          camera={dialogo === 'nuova' ? null : dialogo}
          tipologie={tipologie}
          onClose={() => setDialogo('chiuso')}
        />
      )}
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Tipologie
// ---------------------------------------------------------------------------

function TabTipologie({
  strutturaId,
  tipologie,
  caricamento,
  onErrore,
}: {
  strutturaId: string | null
  tipologie: TipologiaCameraDto[] | undefined
  caricamento: boolean
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | TipologiaCameraDto>('chiuso')
  const elimina = useEliminaTipologia(strutturaId)

  function eliminaTipologia(t: TipologiaCameraDto) {
    if (!window.confirm(`Eliminare la tipologia "${t.tipologiaCamera}"?`)) return
    elimina.mutate(t.id, { onError: onErrore })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Tipologie camera" azione={{ etichetta: '+ Nuova tipologia', onClick: () => setDialogo('nuova'), disabilitato: !strutturaId }} />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell align="right">Prezzo default</TableCell>
                <TableCell align="right">Ospiti inclusi</TableCell>
                <TableCell align="right">Supplemento persona</TableCell>
                <TableCell align="right">Cauzione</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(tipologie ?? []).length === 0 && <RigaVuota colSpan={6} messaggio="Nessuna tipologia configurata." />}
              {(tipologie ?? []).map((t) => (
                <TableRow key={t.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{t.tipologiaCamera}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.prezzoDefault != null ? formattatoreValuta.format(t.prezzoDefault) : '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.numeroImplementoPersona}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {formattatoreValuta.format(t.implemento)}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.cauzione != null ? formattatoreValuta.format(t.cauzione) : '—'}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo(t)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => eliminaTipologia(t)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <TipologiaDialog strutturaId={strutturaId} tipologia={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />
      )}
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Prezzi
// ---------------------------------------------------------------------------

function TabPrezzi({
  strutturaId,
  prezzi,
  caricamento,
  camere,
  tipologie,
  onErrore,
}: {
  strutturaId: string | null
  prezzi: PrezzoCameraDto[] | undefined
  caricamento: boolean
  camere: CameraDto[]
  tipologie: TipologiaCameraDto[]
  onErrore: (err: unknown) => void
}) {
  const [dialogoAperto, setDialogoAperto] = useState(false)
  const elimina = useEliminaPrezzo(strutturaId)

  const nomeCamera = (id: string | null) => camere.find((c) => c.id === id)?.nome ?? null
  const nomeTipologia = (id: string | null) => tipologie.find((t) => t.id === id)?.tipologiaCamera ?? null

  function eliminaPrezzo(p: PrezzoCameraDto) {
    if (!window.confirm('Eliminare questo periodo di prezzo?')) return
    elimina.mutate(p.id, { onError: onErrore })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab
        titolo="Calendario prezzi"
        azione={{ etichetta: '+ Nuovo periodo', onClick: () => setDialogoAperto(true), disabilitato: !strutturaId || (camere.length === 0 && tipologie.length === 0) }}
      />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Ambito</TableCell>
                <TableCell>Dal</TableCell>
                <TableCell>Al</TableCell>
                <TableCell align="right">Prezzo/notte</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(prezzi ?? []).length === 0 && <RigaVuota colSpan={5} messaggio="Nessun periodo di prezzo configurato." />}
              {(prezzi ?? [])
                .slice()
                .sort((a, b) => (a.dataInizio ?? '').localeCompare(b.dataInizio ?? ''))
                .map((p) => (
                  <TableRow key={p.id} hover>
                    <TableCell>
                      {p.cameraId ? `Camera ${nomeCamera(p.cameraId) ?? '—'}` : `Tipologia ${nomeTipologia(p.tipologiaId) ?? '—'}`}
                    </TableCell>
                    <TableCell sx={{ fontFamily: fontMono }}>{p.dataInizio ? formattatoreData.format(new Date(p.dataInizio)) : '—'}</TableCell>
                    <TableCell sx={{ fontFamily: fontMono }}>{p.dataFine ? formattatoreData.format(new Date(p.dataFine)) : '—'}</TableCell>
                    <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                      {p.prezzoPerNotte != null ? formattatoreValuta.format(p.prezzoPerNotte) : '—'}
                    </TableCell>
                    <TableCell align="right">
                      <IconButton size="small" onClick={() => eliminaPrezzo(p)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogoAperto && strutturaId && (
        <PrezzoDialog strutturaId={strutturaId} camere={camere} tipologie={tipologie} onClose={() => setDialogoAperto(false)} />
      )}
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Canali vendita
// ---------------------------------------------------------------------------

function TabCanali({
  strutturaId,
  canali,
  caricamento,
  onErrore,
}: {
  strutturaId: string | null
  canali: CanaleVenditaDto[] | undefined
  caricamento: boolean
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | CanaleVenditaDto>('chiuso')
  const [descrizione, setDescrizione] = useState('')
  const [erroreDialogo, setErroreDialogo] = useState<string | null>(null)

  const crea = useCreaCanaleVendita(strutturaId)
  const aggiorna = useAggiornaCanaleVendita(strutturaId)
  const elimina = useEliminaCanaleVendita(strutturaId)

  function apriDialogo(canale: 'nuovo' | CanaleVenditaDto) {
    setDescrizione(canale === 'nuovo' ? '' : canale.descrizione)
    setErroreDialogo(null)
    setDialogo(canale)
  }

  function salvaCanale() {
    if (descrizione.trim() === '') {
      setErroreDialogo('Il nome del canale è obbligatorio.')
      return
    }
    const onError = (err: unknown) => setErroreDialogo(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    if (dialogo === 'nuovo' || dialogo === 'chiuso') {
      crea.mutate(descrizione.trim(), { onSuccess: () => setDialogo('chiuso'), onError })
    } else {
      aggiorna.mutate({ canaleId: dialogo.id, descrizione: descrizione.trim() }, { onSuccess: () => setDialogo('chiuso'), onError })
    }
  }

  function eliminaCanale(c: CanaleVenditaDto) {
    if (!window.confirm(`Eliminare il canale "${c.descrizione}"?`)) return
    elimina.mutate(c.id, { onError: onErrore })
  }

  const inCorso = crea.isPending || aggiorna.isPending

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Canali vendita" azione={{ etichetta: '+ Nuovo canale', onClick: () => apriDialogo('nuovo'), disabilitato: !strutturaId }} />

      {caricamento && <Skeleton variant="rounded" height={160} />}

      {!caricamento && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Descrizione</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(canali ?? []).length === 0 && <RigaVuota colSpan={2} messaggio="Nessun canale configurato." />}
              {(canali ?? []).map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{c.descrizione}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => apriDialogo(c)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => eliminaCanale(c)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && (
        <Dialog open onClose={() => setDialogo('chiuso')} maxWidth="xs" fullWidth>
          <DialogTitle>{dialogo === 'nuovo' ? 'Nuovo canale vendita' : 'Modifica canale vendita'}</DialogTitle>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            {erroreDialogo && <Alert severity="error">{erroreDialogo}</Alert>}
            <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} autoFocus disabled={inCorso} />
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 2.5 }}>
            <Button onClick={() => setDialogo('chiuso')} disabled={inCorso}>
              Chiudi
            </Button>
            <Button variant="contained" color="secondary" onClick={salvaCanale} disabled={inCorso}>
              Salva
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  )
}
