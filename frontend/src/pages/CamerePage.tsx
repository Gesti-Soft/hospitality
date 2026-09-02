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
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import { aggiungiGiorni, differenzaGiorni, inizioGiornoLocale } from '../lib/date'
import { useStruttura } from '../struttura/StrutturaContext'
import { StatoCamera, useCamere, useDuplicaCamere, useEliminaCamera, type CameraDto } from '../api/camere'
import { useTipologie, type TipologiaCameraDto } from '../api/tipologie'
import { usePrezzi, useEliminaPrezzo, type PrezzoCameraDto } from '../api/prezzi'
import { useCanaliVendita, useCreaCanaleVendita, useAggiornaCanaleVendita, useEliminaCanaleVendita, type CanaleVenditaDto } from '../api/canaliVendita'
import { ApiError } from '../api/client'
import { fontDisplay, fontMono, tokens } from '../theme'
import { CameraDialog } from '../components/CameraDialog'
import { PrezzoDialog } from '../components/PrezzoDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'

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

type Tab_ = 'camere' | 'prezzi' | 'canali'

export function CamerePage() {
  const { strutturaId, strutture } = useStruttura()
  const [tab, setTab] = useState<Tab_>('camere')
  const [tipologiaSelezionataId, setTipologiaSelezionataId] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [duplicaAperto, setDuplicaAperto] = useState(false)

  const camere = useCamere(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const prezzi = usePrezzi(strutturaId)
  const canali = useCanaliVendita(strutturaId)

  // La select Tipologia governa Camere/Prezzi (Canali vendita non dipende dalla tipologia, ma
  // resta comunque dietro la stessa selezione): di default è sempre popolata con la prima
  // disponibile, derivata al volo così segue automaticamente eliminazioni/cambio struttura senza
  // un effetto dedicato.
  const listaTipologie = tipologie.data ?? []
  const tipologiaId = listaTipologie.some((t) => t.id === tipologiaSelezionataId) ? tipologiaSelezionataId : (listaTipologie[0]?.id ?? '')

  function segnalaErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  const altreStrutture = strutture.filter((s) => s.id !== strutturaId)
  const nessunaTipologia = !tipologie.isLoading && listaTipologie.length === 0

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      {!nessunaTipologia && (
        <TextField
          select
          size="small"
          label="Tipologia"
          value={tipologiaId}
          onChange={(e) => setTipologiaSelezionataId(e.target.value)}
          disabled={tipologie.isLoading}
          sx={{ alignSelf: 'flex-start', width: 220 }}
        >
          {listaTipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              {t.tipologiaCamera}
            </MenuItem>
          ))}
        </TextField>
      )}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ minHeight: 0 }}>
          <Tab label="Camere" value="camere" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Prezzi" value="prezzi" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Canali vendita" value="canali" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        </Tabs>
        {altreStrutture.length > 0 && (
          <Button variant="outlined" size="small" onClick={() => setDuplicaAperto(true)}>
            Duplica da un'altra struttura
          </Button>
        )}
      </Box>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {nessunaTipologia ? (
        <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 4, textAlign: 'center', color: tokens.textSecondary }}>
          Nessuna tipologia configurata. Vai su "Tipologie" nel menu per crearne una prima di gestire camere, prezzi e canali vendita.
        </Box>
      ) : (
        <>
          {tab === 'camere' && (
            <TabCamere
              strutturaId={strutturaId}
              camere={camere.data}
              caricamento={camere.isLoading}
              tipologie={tipologie.data ?? []}
              tipologiaId={tipologiaId}
              onErrore={segnalaErrore}
            />
          )}
          {tab === 'prezzi' && (
            <TabPrezzi
              strutturaId={strutturaId}
              prezzi={prezzi.data}
              caricamento={prezzi.isLoading}
              camere={camere.data ?? []}
              tipologie={tipologie.data ?? []}
              tipologiaId={tipologiaId}
              onErrore={segnalaErrore}
            />
          )}
          {tab === 'canali' && <TabCanali strutturaId={strutturaId} canali={canali.data} caricamento={canali.isLoading} onErrore={segnalaErrore} />}
        </>
      )}

      {duplicaAperto && strutturaId && <DuplicaDaAltraStrutturaDialog strutturaId={strutturaId} altreStrutture={altreStrutture} onClose={() => setDuplicaAperto(false)} />}
    </Box>
  )
}

function DuplicaDaAltraStrutturaDialog({
  strutturaId,
  altreStrutture,
  onClose,
}: {
  strutturaId: string
  altreStrutture: { id: string; nome: string }[]
  onClose: () => void
}) {
  const [origineId, setOrigineId] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [risultato, setRisultato] = useState<{ tipologie: number; camere: number; prezzi: number; canali: number; saltati: number } | null>(null)

  const duplica = useDuplicaCamere(strutturaId)

  function conferma() {
    if (origineId === '') {
      setErrore('Seleziona la struttura da cui duplicare.')
      return
    }
    setErrore(null)
    duplica.mutate(origineId, {
      onSuccess: (r) => setRisultato(r),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Duplica da un'altra struttura</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        {risultato ? (
          <Alert severity="success">
            Duplicati: {risultato.tipologie} tipologie, {risultato.camere} camere, {risultato.prezzi} periodi di prezzo, {risultato.canali} canali
            vendita.
            {risultato.saltati > 0 && ` ${risultato.saltati} elemento/i saltato/i perché già esistente/i con lo stesso nome in questa struttura.`}
          </Alert>
        ) : (
          <>
            <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
              Copia tipologie, camere, periodi di prezzo e canali vendita dalla struttura scelta a questa. Gli elementi il cui nome esiste già
              qui vengono saltati, non sovrascritti.
            </Typography>
            <TextField select label="Struttura di origine" value={origineId} onChange={(e) => setOrigineId(e.target.value)} disabled={duplica.isPending}>
              {altreStrutture.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.nome}
                </MenuItem>
              ))}
            </TextField>
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={duplica.isPending}>
          {risultato ? 'Chiudi' : 'Annulla'}
        </Button>
        {!risultato && (
          <Button variant="contained" color="secondary" onClick={conferma} disabled={duplica.isPending}>
            Duplica
          </Button>
        )}
      </DialogActions>
    </Dialog>
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
  tipologiaId,
  onErrore,
}: {
  strutturaId: string | null
  camere: CameraDto[] | undefined
  caricamento: boolean
  tipologie: TipologiaCameraDto[]
  tipologiaId: string
  onErrore: (err: unknown) => void
}) {
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | CameraDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<CameraDto | null>(null)
  const elimina = useEliminaCamera(strutturaId)

  function confermaEliminaCamera() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null), onError: onErrore })
  }

  const camereFiltrate = (camere ?? []).filter((c) => c.tipologiaId === tipologiaId)

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
                <TableCell>Stato</TableCell>
                <TableCell align="right">Capacità</TableCell>
                <TableCell align="right">Soggiorno minimo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {camereFiltrate.length === 0 && <RigaVuota colSpan={5} messaggio="Nessuna camera per questa tipologia." />}
              {camereFiltrate.map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{c.nome}</TableCell>
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
                    <IconButton size="small" onClick={() => setDaEliminare(c)}>
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
          tipologiaDiDefault={tipologiaId}
          onClose={() => setDialogo('chiuso')}
        />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare camera"
          messaggio={`Eliminare la camera "${daEliminare.nome}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaCamera}
          onAnnulla={() => setDaEliminare(null)}
        />
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
  tipologiaId,
  onErrore,
}: {
  strutturaId: string | null
  prezzi: PrezzoCameraDto[] | undefined
  caricamento: boolean
  camere: CameraDto[]
  tipologie: TipologiaCameraDto[]
  tipologiaId: string
  onErrore: (err: unknown) => void
}) {
  const [dialogoAperto, setDialogoAperto] = useState(false)
  const [vista, setVista] = useState<'lista' | 'calendario'>('lista')
  const [daEliminare, setDaEliminare] = useState<PrezzoCameraDto | null>(null)
  const elimina = useEliminaPrezzo(strutturaId)

  const camereTipologia = camere.filter((c) => c.tipologiaId === tipologiaId)
  const prezziTipologia = (prezzi ?? []).filter(
    (p) => p.tipologiaId === tipologiaId || camereTipologia.some((c) => c.id === p.cameraId),
  )

  const nomeCamera = (id: string | null) => camere.find((c) => c.id === id)?.nome ?? null
  const nomeTipologia = (id: string | null) => tipologie.find((t) => t.id === id)?.tipologiaCamera ?? null

  function eliminaPrezzo(p: PrezzoCameraDto) {
    setDaEliminare(p)
  }

  function confermaEliminaPrezzo() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null), onError: onErrore })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Calendario prezzi</Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <ToggleButtonGroup exclusive size="small" value={vista} onChange={(_, v) => v && setVista(v)}>
            <ToggleButton value="lista">Lista</ToggleButton>
            <ToggleButton value="calendario">Calendario</ToggleButton>
          </ToggleButtonGroup>
          <Button
            variant="contained"
            color="secondary"
            size="small"
            onClick={() => setDialogoAperto(true)}
            disabled={!strutturaId || tipologie.length === 0}
          >
            + Nuovo periodo
          </Button>
        </Box>
      </Box>

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && vista === 'lista' && (
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
              {prezziTipologia.length === 0 && <RigaVuota colSpan={5} messaggio="Nessun periodo di prezzo configurato per questa tipologia." />}
              {prezziTipologia
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

      {!caricamento && vista === 'calendario' && (
        <VistaPrezziCalendario
          prezzi={prezziTipologia}
          camere={camereTipologia}
          tipologie={tipologie.filter((t) => t.id === tipologiaId)}
          onEliminaPrezzo={eliminaPrezzo}
        />
      )}

      {dialogoAperto && strutturaId && (
        <PrezzoDialog strutturaId={strutturaId} camere={camere} tipologie={tipologie} onClose={() => setDialogoAperto(false)} />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare periodo di prezzo"
          messaggio="Eliminare questo periodo di prezzo?"
          inCorso={elimina.isPending}
          onConferma={confermaEliminaPrezzo}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}

const GIORNI_VISIBILI_PREZZI = 14
const COL_RIGA_PREZZI = 200
const COL_GIORNO_PREZZI = 74

function VistaPrezziCalendario({
  prezzi,
  camere,
  tipologie,
  onEliminaPrezzo,
}: {
  prezzi: PrezzoCameraDto[]
  camere: CameraDto[]
  tipologie: TipologiaCameraDto[]
  onEliminaPrezzo: (p: PrezzoCameraDto) => void
}) {
  const [inizioFinestra, setInizioFinestra] = useState(() => inizioGiornoLocale(new Date()))
  const giorni = Array.from({ length: GIORNI_VISIBILI_PREZZI }, (_, i) => aggiungiGiorni(inizioFinestra, i))

  // Una riga per tipologia (ambito "tutta la tipologia") + una riga per ogni camera che ha almeno
  // un periodo specifico — evita di elencare tutte le camere della struttura se non hanno prezzi propri.
  type Riga = { chiave: string; etichetta: string; cameraId: string | null; tipologiaId: string | null }
  const righe: Riga[] = [
    ...tipologie.map((t) => ({ chiave: `t-${t.id}`, etichetta: t.tipologiaCamera, cameraId: null, tipologiaId: t.id })),
    ...camere
      .filter((c) => prezzi.some((p) => p.cameraId === c.id))
      .map((c) => ({ chiave: `c-${c.id}`, etichetta: `${c.nome} (camera)`, cameraId: c.id, tipologiaId: null })),
  ]

  function prezzoDelGiorno(riga: Riga, giorno: Date): PrezzoCameraDto | null {
    return (
      prezzi.find((p) => {
        if (riga.cameraId ? p.cameraId !== riga.cameraId : p.tipologiaId !== riga.tipologiaId) return false
        if (!p.dataInizio || !p.dataFine) return false
        const inizio = inizioGiornoLocale(new Date(p.dataInizio))
        const fine = inizioGiornoLocale(new Date(p.dataFine))
        return giorno >= inizio && giorno <= fine
      }) ?? null
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, -7))}>
          <ChevronLeftIcon fontSize="small" />
        </IconButton>
        <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 190, textAlign: 'center' }}>
          {formattatoreData.format(giorni[0])} – {formattatoreData.format(giorni[giorni.length - 1])}
        </Typography>
        <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, 7))}>
          <ChevronRightIcon fontSize="small" />
        </IconButton>
      </Box>

      {righe.length === 0 && (
        <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 4, textAlign: 'center', color: tokens.textSecondary }}>
          Nessuna tipologia configurata.
        </Box>
      )}

      {righe.length > 0 && (
        <Cornice>
          <Box sx={{ overflowX: 'auto' }}>
            <Box sx={{ display: 'flex' }}>
              <Box sx={{ width: COL_RIGA_PREZZI, flex: '0 0 auto', borderRight: `1px solid ${tokens.surfaceBorder}` }} />
              <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${GIORNI_VISIBILI_PREZZI}, ${COL_GIORNO_PREZZI}px)` }}>
                {giorni.map((g) => {
                  const weekend = g.getDay() === 0 || g.getDay() === 6
                  const oggi = differenzaGiorni(g, new Date()) === 0
                  return (
                    <Box
                      key={g.getTime()}
                      sx={{
                        height: 36,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        bgcolor: weekend ? tokens.paper : 'transparent',
                        borderLeft: `1px solid ${tokens.surfaceBorder}`,
                        borderBottom: `1px solid ${tokens.surfaceBorder}`,
                      }}
                    >
                      <Typography sx={{ fontFamily: fontMono, fontSize: 12, fontWeight: oggi ? 700 : 500, color: oggi ? tokens.blue600 : tokens.textSecondary }}>
                        {g.getDate()}/{g.getMonth() + 1}
                      </Typography>
                    </Box>
                  )
                })}
              </Box>
            </Box>

            {righe.map((riga) => (
              <Box key={riga.chiave} sx={{ display: 'flex', borderTop: `1px solid ${tokens.surfaceBorder}` }}>
                <Box sx={{ width: COL_RIGA_PREZZI, flex: '0 0 auto', display: 'flex', alignItems: 'center', px: 1.5, borderRight: `1px solid ${tokens.surfaceBorder}` }}>
                  <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 700 }}>
                    {riga.etichetta}
                  </Typography>
                </Box>
                <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${GIORNI_VISIBILI_PREZZI}, ${COL_GIORNO_PREZZI}px)` }}>
                  {giorni.map((g) => {
                    const p = prezzoDelGiorno(riga, g)
                    return (
                      <Box
                        key={g.getTime()}
                        onClick={() => p && onEliminaPrezzo(p)}
                        title={p ? 'Clicca per eliminare questo periodo' : undefined}
                        sx={{
                          height: 40,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          borderLeft: `1px solid ${tokens.surfaceBorder}`,
                          bgcolor: p ? tokens.ok100 : 'transparent',
                          cursor: p ? 'pointer' : 'default',
                        }}
                      >
                        {p?.prezzoPerNotte != null && (
                          <Typography sx={{ fontFamily: fontMono, fontSize: 11.5, fontWeight: 700, color: tokens.ok600 }}>
                            {Math.round(p.prezzoPerNotte)}€
                          </Typography>
                        )}
                      </Box>
                    )
                  })}
                </Box>
              </Box>
            ))}
          </Box>
        </Cornice>
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
  const [daEliminare, setDaEliminare] = useState<CanaleVenditaDto | null>(null)

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

  function confermaEliminaCanale() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null), onError: onErrore })
  }

  const inCorso = crea.isPending || aggiorna.isPending

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab titolo="Canali vendita" azione={{ etichetta: '+ Nuovo canale', onClick: () => apriDialogo('nuovo'), disabilitato: !strutturaId }} />
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Le fonti di provenienza delle prenotazioni (es. Booking.com, Diretta) — selezionabili quando crei una prenotazione e usate per
        colorare i pallini nel Calendario.
      </Typography>

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
                    <IconButton size="small" onClick={() => setDaEliminare(c)}>
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

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare canale vendita"
          messaggio={`Eliminare il canale "${daEliminare.descrizione}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaCanale}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
