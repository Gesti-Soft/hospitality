import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import LinkIcon from '@mui/icons-material/LinkOutlined'
import LinkOffIcon from '@mui/icons-material/LinkOffOutlined'
import EditCalendarIcon from '@mui/icons-material/EditCalendarOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import SettingsIcon from '@mui/icons-material/SettingsOutlined'
import MoreVertIcon from '@mui/icons-material/MoreVertOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useTipologie } from '../../api/tipologie'
import { ApiError } from '../../api/client'
import {
  useAssociaCameraWubook,
  useCamerePerAssociazione,
  useCamereRemoteWubook,
  useEliminaPianoPrezzo,
  useEliminaPianoRestrizione,
  usePianiPrezzo,
  usePianiRestrizione,
  useRimuoviWubookCamera,
  useSincronizzaWubookDisponibilita,
  useSincronizzaWubookPrenotazioni,
  useSincronizzaWubookPrezzi,
  useWubookConfig,
  type CameraWubookInfoDto,
  type CameraWubookRemoteDto,
  type PianoPrezzoDto,
  type PianoRestrizioneDto,
  type WubookIntegrazioneDto,
} from '../../api/integrazioni'
import { aggiungiGiorni, formatoInputData, isoLocale, parsaInputData } from '../../lib/date'
import { useMobile } from '../../lib/useMobile'
import { CampoData } from '../../components/CampoData'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { ChiusureRestrizioniDialog } from '../../components/ChiusureRestrizioniDialog'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { ImpostazioniWubookCameraDialog } from '../../components/ImpostazioniWubookCameraDialog'
import { PianoPrezzoDialog } from '../../components/PianoPrezzoDialog'
import { PianoRestrizioneDialog } from '../../components/PianoRestrizioneDialog'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

type TabWubook = 'camere' | 'piani-prezzo' | 'piani-restrizione'

export function WubookPage() {
  const { strutturaId } = useStruttura()
  const config = useWubookConfig(strutturaId)
  const camere = useCamerePerAssociazione(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const [tab, setTab] = useState<TabWubook>('camere')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 1000 }}>

      {config.isLoading && <Skeleton variant="rounded" height={80} />}
      {!config.isLoading && config.data && <StatoWubook dati={config.data} />}

      {!config.isLoading && config.data?.credenzialiPronte && <SincronizzazioneForm strutturaId={strutturaId!} />}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ minHeight: 0 }}>
        <Tab label="Camere" value="camere" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Piani prezzo" value="piani-prezzo" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Piani restrizione" value="piani-restrizione" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
      </Tabs>

      {tab === 'camere' && (
        <Box>
          {camere.isLoading && <Skeleton variant="rounded" height={180} />}
          {!camere.isLoading && <TabellaCamere strutturaId={strutturaId!} camere={camere.data ?? []} tipologie={tipologie.data ?? []} />}
        </Box>
      )}

      {tab === 'piani-prezzo' && <TabPianiPrezzo strutturaId={strutturaId!} />}
      {tab === 'piani-restrizione' && <TabPianiRestrizione strutturaId={strutturaId!} />}
    </Box>
  )
}

function StatoWubook({ dati }: { dati: WubookIntegrazioneDto }) {
  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Stato sincronizzazione</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Chip size="small" label={dati.attivo ? 'Attiva' : 'Non attiva'} sx={{ bgcolor: dati.attivo ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
          <Chip size="small" label={dati.credenzialiPronte ? 'Credenziali pronte' : 'In attesa di rinnovo'} sx={{ bgcolor: dati.credenzialiPronte ? tokens.blue600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
        </Box>
      </Box>
      {dati.ultimoErrore && <Alert severity="warning">{dati.ultimoErrore}</Alert>}
    </Box>
  )
}

function SincronizzazioneForm({ strutturaId }: { strutturaId: string }) {
  const mobile = useMobile()
  const puoScrivereCamere = usePuoScrivere('settingRoomWrite')
  const puoScrivereRenotazioni = usePuoScrivere('reservationWrite')
  const oggi = new Date()
  const [dataInizio, setDataInizio] = useState(formatoInputData(oggi))
  const [dataFine, setDataFine] = useState(formatoInputData(aggiungiGiorni(oggi, 30)))
  const [messaggio, setMessaggio] = useState<string | null>(null)
  const toast = useToast()

  const sincronizzaPrezzi = useSincronizzaWubookPrezzi(strutturaId)
  const sincronizzaDisponibilita = useSincronizzaWubookDisponibilita(strutturaId)
  const sincronizzaPrenotazioni = useSincronizzaWubookPrenotazioni(strutturaId)

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function esegui(azione: 'prezzi' | 'disponibilita' | 'prenotazioni') {
    setMessaggio(null)
    const periodo = { dataInizio: isoLocale(parsaInputData(dataInizio)), dataFine: isoLocale(parsaInputData(dataFine)) }

    if (azione === 'prezzi') {
      sincronizzaPrezzi.mutate(periodo, { onSuccess: () => setMessaggio('Prezzi sincronizzati.'), onError: gestisciErrore })
    } else if (azione === 'disponibilita') {
      sincronizzaDisponibilita.mutate(periodo, {
        onSuccess: () => setMessaggio('Disponibilità sincronizzata (chiusure e restrizioni per periodo incluse).'),
        onError: gestisciErrore,
      })
    } else {
      sincronizzaPrenotazioni.mutate(undefined, {
        onSuccess: (r) =>
          setMessaggio(
            `Prenotazioni: ${r.importate} importate, ${r.aggiornate} aggiornate, ${r.annullate} annullate.${r.errori > 0 ? ` ${r.errori} errore/i.` : ''}`,
          ),
        onError: gestisciErrore,
      })
    }
  }

  const inCorso = sincronizzaPrezzi.isPending || sincronizzaDisponibilita.isPending || sincronizzaPrenotazioni.isPending

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Sincronizzazione manuale</Typography>

      {messaggio && <Alert severity="success" onClose={() => setMessaggio(null)}>{messaggio}</Alert>}

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <CampoData label="Dal" value={dataInizio} onChange={setDataInizio} fullWidth disabled={inCorso} />
        <CampoData label="Al" value={dataFine} onChange={setDataFine} min={dataInizio || undefined} fullWidth disabled={inCorso} />
      </Box>

      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        {puoScrivereCamere && (
          <>
            <Button variant="outlined" onClick={() => esegui('prezzi')} disabled={inCorso}>
              Sincronizza prezzi
            </Button>
            <Button variant="outlined" onClick={() => esegui('disponibilita')} disabled={inCorso}>
              Sincronizza disponibilità
            </Button>
          </>
        )}
        {puoScrivereRenotazioni && (
          <Button variant="outlined" onClick={() => esegui('prenotazioni')} disabled={inCorso}>
            Sincronizza prenotazioni (pull)
          </Button>
        )}
      </Box>
    </Box>
  )
}

function TabellaCamere({
  strutturaId,
  camere,
  tipologie,
}: {
  strutturaId: string
  camere: CameraWubookInfoDto[]
  tipologie: { id: string; tipologiaCamera: string }[]
}) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const [tipologiaFiltro, setTipologiaFiltro] = useState('')
  const [dialogoChiusure, setDialogoChiusure] = useState<{ cameraId: string; cameraNome: string } | null>(null)
  const [dialogoAssocia, setDialogoAssocia] = useState<CameraWubookInfoDto | null>(null)
  const [dialogoImpostazioni, setDialogoImpostazioni] = useState<CameraWubookInfoDto | null>(null)
  const [dialogoNuova, setDialogoNuova] = useState(false)
  const [daEliminare, setDaEliminare] = useState<CameraWubookInfoDto | null>(null)
  const [daDisassociare, setDaDisassociare] = useState<CameraWubookInfoDto | null>(null)
  const rimuovi = useRimuoviWubookCamera(strutturaId)
  const disassocia = useAssociaCameraWubook(strutturaId)
  const toast = useToast()

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function confermaEliminaDaWubook() {
    if (!daEliminare) return
    rimuovi.mutate(daEliminare.cameraId, { onSuccess: () => setDaEliminare(null), onError: gestisciErrore })
  }

  // Disassocia = scollega la camera locale da quella su OTA senza toccare OTA (nessun del_room): la
  // room resta lì intatta, così si può riassociarla in seguito (anche a un'altra camera locale)
  // scegliendola da "Associa a una camera già esistente" — a differenza di "Elimina da OTA", che
  // invece la cancella davvero.
  function confermaDisassocia() {
    if (!daDisassociare) return
    disassocia.mutate(
      { cameraId: daDisassociare.cameraId, idCameraWubook: null },
      { onSuccess: () => setDaDisassociare(null), onError: gestisciErrore },
    )
  }

  // Come otaservice.web (legacy): select Tipologia prima di tutto, poi solo le camere di quella
  // tipologia — mai la tabella piatta con tutte le camere della struttura insieme.
  const camereFiltrate = camere.filter((c) => tipologiaFiltro === '' || c.tipologiaId === tipologiaFiltro)
  // Come il bottone globale "Nuova Camera" del vecchio programma: crea su OTA una camera locale
  // esistente non ancora associata — non serve più passare dalla riga della tabella per scoprire
  // che l'azione esiste.
  const camereDaCreare = camere.filter((c) => !c.wubookAttiva)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <TextField select size="small" label="Tipologia" value={tipologiaFiltro} onChange={(e) => setTipologiaFiltro(e.target.value)} sx={{ minWidth: 240 }}>
          <MenuItem value="">Tutte le tipologie</MenuItem>
          {tipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              {t.tipologiaCamera}
            </MenuItem>
          ))}
        </TextField>
        {puoScrivere && <BottoneNuovo etichetta="+ Nuova camera" onClick={() => setDialogoNuova(true)} disabilitato={camereDaCreare.length === 0} />}
      </Box>

      {mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {camereFiltrate.length === 0 && (
            <MessaggioVuotoElenco messaggio={tipologiaFiltro === '' ? 'Nessuna camera configurata.' : 'Nessuna camera per questa tipologia.'} />
          )}
          {camereFiltrate.map((c) => (
            <CardElenco key={c.cameraId} coloreAccento={c.chiusaOggi ? tokens.error600 : tokens.ok600}>
              <TestataCardElenco
                titolo={c.cameraNome}
                sottotitolo={c.tipologiaNome ?? undefined}
                azioneDestra={
                  c.wubookAttiva && c.idCameraWubook != null ? (
                    <Chip size="small" label={`Associata (id ${c.idCameraWubook})`} sx={{ bgcolor: tokens.ok600, color: '#fff', fontWeight: 700 }} />
                  ) : (
                    <Chip size="small" label="Non associata" sx={{ bgcolor: tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
                  )
                }
              />
              <RigaCardMeta
                voci={[
                  {
                    etichetta: 'Disponibilità oggi',
                    valore: (
                      <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                        <Chip
                          size="small"
                          label={c.chiusaOggi ? 'Chiusa' : 'Disponibile'}
                          sx={{ bgcolor: c.chiusaOggi ? tokens.error600 : tokens.ok600, color: '#fff', fontWeight: 700 }}
                        />
                        {c.chiusureCount > 0 && (
                          <Chip size="small" label={`Chiusure: ${c.chiusureCount}`} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />
                        )}
                        {c.restrizioniCount > 0 && (
                          <Chip size="small" label={`Restrizioni: ${c.restrizioniCount}`} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />
                        )}
                      </Box>
                    ),
                  },
                ]}
              />
              {(c.wubookAttiva || puoScrivere) && (
                <AzioniCardElenco>
                  <MenuAzioniCamera
                    camera={c}
                    puoScrivere={puoScrivere}
                    onAssocia={() => setDialogoAssocia(c)}
                    onImpostazioni={() => setDialogoImpostazioni(c)}
                    onChiusure={() => setDialogoChiusure({ cameraId: c.cameraId, cameraNome: c.cameraNome })}
                    onDisassocia={() => setDaDisassociare(c)}
                    onElimina={() => setDaEliminare(c)}
                    disassociaInCorso={disassocia.isPending}
                    eliminaInCorso={rimuovi.isPending}
                  />
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
        </Box>
      )}

      {!mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Camera</TableCell>
                <TableCell>Tipologia</TableCell>
                <TableCell>Disponibilità oggi</TableCell>
                <TableCell>Associazione OTA</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {camereFiltrate.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    {tipologiaFiltro === '' ? 'Nessuna camera configurata.' : 'Nessuna camera per questa tipologia.'}
                  </TableCell>
                </TableRow>
              )}
              {camereFiltrate.map((c) => (
                <TableRow key={c.cameraId} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{c.cameraNome}</TableCell>
                  <TableCell>{c.tipologiaNome ?? '—'}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                      <Chip
                        size="small"
                        label={c.chiusaOggi ? 'Chiusa' : 'Disponibile'}
                        sx={{ bgcolor: c.chiusaOggi ? tokens.error600 : tokens.ok600, color: '#fff', fontWeight: 700 }}
                      />
                      {c.chiusureCount > 0 && (
                        <Chip size="small" label={`Chiusure: ${c.chiusureCount}`} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />
                      )}
                      {c.restrizioniCount > 0 && (
                        <Chip size="small" label={`Restrizioni: ${c.restrizioniCount}`} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />
                      )}
                    </Box>
                  </TableCell>
                  <TableCell>
                    {c.wubookAttiva && c.idCameraWubook != null ? (
                      <Chip size="small" label={`Associata (id ${c.idCameraWubook})`} sx={{ bgcolor: tokens.ok600, color: '#fff', fontWeight: 700 }} />
                    ) : (
                      <Chip size="small" label="Non associata" sx={{ bgcolor: tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
                    )}
                  </TableCell>
                  <TableCell align="right">
                    <MenuAzioniCamera
                      camera={c}
                      puoScrivere={puoScrivere}
                      onAssocia={() => setDialogoAssocia(c)}
                      onImpostazioni={() => setDialogoImpostazioni(c)}
                      onChiusure={() => setDialogoChiusure({ cameraId: c.cameraId, cameraNome: c.cameraNome })}
                      onDisassocia={() => setDaDisassociare(c)}
                      onElimina={() => setDaEliminare(c)}
                      disassociaInCorso={disassocia.isPending}
                      eliminaInCorso={rimuovi.isPending}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogoChiusure && (
        <ChiusureRestrizioniDialog
          strutturaId={strutturaId}
          cameraId={dialogoChiusure.cameraId}
          cameraNome={dialogoChiusure.cameraNome}
          onClose={() => setDialogoChiusure(null)}
        />
      )}

      {dialogoAssocia && (
        <AssociaCameraWubookDialog strutturaId={strutturaId} camera={dialogoAssocia} onClose={() => setDialogoAssocia(null)} />
      )}

      {dialogoNuova && (
        <NuovaCameraOtaDialog
          camere={camereDaCreare}
          onSeleziona={(c) => {
            setDialogoNuova(false)
            setDialogoImpostazioni(c)
          }}
          onClose={() => setDialogoNuova(false)}
        />
      )}

      {dialogoImpostazioni && (
        <ImpostazioniWubookCameraDialog
          strutturaId={strutturaId}
          cameraId={dialogoImpostazioni.cameraId}
          cameraNome={dialogoImpostazioni.cameraNome}
          wubookAttiva={dialogoImpostazioni.wubookAttiva}
          idCameraWubook={dialogoImpostazioni.idCameraWubook}
          onClose={() => setDialogoImpostazioni(null)}
        />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare da OTA"
          messaggio={`Eliminare "${daEliminare.cameraNome}" da OTA? La camera locale resta, solo la camera sull'OTA viene rimossa.`}
          inCorso={rimuovi.isPending}
          onConferma={confermaEliminaDaWubook}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}

      {daDisassociare && (
        <ConfirmDialog
          titolo="Disassociare da OTA"
          messaggio={`Disassociare "${daDisassociare.cameraNome}" da OTA? La camera resta invariata sull'OTA (non viene eliminata) — potrai riassociarla in seguito da "Associa a una camera già esistente".`}
          inCorso={disassocia.isPending}
          onConferma={confermaDisassocia}
          onAnnulla={() => setDaDisassociare(null)}
        />
      )}
    </Box>
  )
}

/** Azioni per riga camera raccolte in un menu a tendina (icona "···") invece di una fila di icone — la stessa riga arriva ad avere fino a 4 azioni possibili (chiusure, modifica, disassocia, elimina), troppe per stare bene affiancate. */
function MenuAzioniCamera({
  camera,
  puoScrivere,
  onAssocia,
  onImpostazioni,
  onChiusure,
  onDisassocia,
  onElimina,
  disassociaInCorso,
  eliminaInCorso,
}: {
  camera: CameraWubookInfoDto
  puoScrivere: boolean
  onAssocia: () => void
  onImpostazioni: () => void
  onChiusure: () => void
  onDisassocia: () => void
  onElimina: () => void
  disassociaInCorso: boolean
  eliminaInCorso: boolean
}) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)

  if (!camera.wubookAttiva && !puoScrivere) return null

  function esegui(azione: () => void) {
    setAnchorEl(null)
    azione()
  }

  return (
    <>
      <IconButton size="small" onClick={(e) => setAnchorEl(e.currentTarget)}>
        <MoreVertIcon fontSize="small" />
      </IconButton>
      <Menu anchorEl={anchorEl} open={!!anchorEl} onClose={() => setAnchorEl(null)}>
        {!camera.wubookAttiva && puoScrivere && (
          <MenuItem onClick={() => esegui(onAssocia)}>
            <ListItemIcon>
              <LinkIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Associa a una camera già esistente su OTA</ListItemText>
          </MenuItem>
        )}
        {!camera.wubookAttiva && puoScrivere && (
          <MenuItem onClick={() => esegui(onImpostazioni)}>
            <ListItemIcon>
              <SettingsIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Crea su OTA</ListItemText>
          </MenuItem>
        )}
        {camera.wubookAttiva && (
          <MenuItem onClick={() => esegui(onChiusure)}>
            <ListItemIcon>
              <EditCalendarIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Chiusure e restrizioni per periodo</ListItemText>
          </MenuItem>
        )}
        {camera.wubookAttiva && puoScrivere && (
          <MenuItem onClick={() => esegui(onImpostazioni)}>
            <ListItemIcon>
              <SettingsIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Modifica su OTA</ListItemText>
          </MenuItem>
        )}
        {camera.wubookAttiva && puoScrivere && (
          <MenuItem onClick={() => esegui(onDisassocia)} disabled={disassociaInCorso}>
            <ListItemIcon>
              <LinkOffIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Disassocia</ListItemText>
          </MenuItem>
        )}
        {camera.wubookAttiva && puoScrivere && (
          <MenuItem onClick={() => esegui(onElimina)} disabled={eliminaInCorso}>
            <ListItemIcon>
              <DeleteIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Elimina da OTA</ListItemText>
          </MenuItem>
        )}
      </Menu>
    </>
  )
}

function AssociaCameraWubookDialog({ strutturaId, camera, onClose }: { strutturaId: string; camera: CameraWubookInfoDto; onClose: () => void }) {
  const remote = useCamereRemoteWubook(strutturaId, true)
  const associa = useAssociaCameraWubook(strutturaId)
  const [selezionata, setSelezionata] = useState<CameraWubookRemoteDto | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  function conferma() {
    if (!selezionata) {
      setErrore('Seleziona una camera OTA.')
      return
    }
    setErrore(null)
    associa.mutate({ cameraId: camera.cameraId, idCameraWubook: selezionata.id }, { onSuccess: onClose, onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') })
  }

  function rimuoviAssociazione() {
    setErrore(null)
    associa.mutate({ cameraId: camera.cameraId, idCameraWubook: null }, { onSuccess: onClose, onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Associa "{camera.cameraNome}" a OTA</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Scegli la camera già presente sull'OTA a cui corrisponde questa camera locale — nessuna nuova camera viene creata sull'OTA,
          solo l'associazione viene salvata (a differenza di "Crea su OTA", che invece crea una camera nuova).
        </Typography>
        {remote.isLoading && <Skeleton variant="rounded" height={56} />}
        {remote.isError && <Alert severity="error">Impossibile recuperare le camere dall'OTA. Verifica le credenziali in Impostazioni.</Alert>}
        {!remote.isLoading && !remote.isError && (
          <Autocomplete
            options={remote.data ?? []}
            getOptionLabel={(r) => `${r.nome} (id ${r.id})`}
            value={selezionata}
            onChange={(_, valore) => setSelezionata(valore)}
            disabled={associa.isPending}
            noOptionsText="Nessuna camera trovata sull'OTA"
            renderInput={(params) => <TextField {...params} label="Camera OTA" placeholder="Cerca per nome…" autoFocus />}
          />
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        {camera.wubookAttiva && (
          <Button color="error" onClick={rimuoviAssociazione} disabled={associa.isPending} sx={{ mr: 'auto' }}>
            Rimuovi associazione
          </Button>
        )}
        <Button onClick={onClose} disabled={associa.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={conferma} disabled={associa.isPending || remote.isLoading}>
          Associa
        </Button>
      </DialogActions>
    </Dialog>
  )
}

/**
 * Come il bottone globale "Nuova Camera" del vecchio programma (otaservice.web): un solo punto di
 * ingresso per creare una camera su OTA, che parte scegliendo la camera locale già esistente da
 * cui prendere nome/tipologia — non serve più andare a cercare la riga giusta in tabella. Dopo la
 * scelta si riusa lo stesso dialog "Impostazioni OTA" già usato dall'azione per-riga.
 */
function NuovaCameraOtaDialog({
  camere,
  onSeleziona,
  onClose,
}: {
  camere: CameraWubookInfoDto[]
  onSeleziona: (camera: CameraWubookInfoDto) => void
  onClose: () => void
}) {
  const [selezionata, setSelezionata] = useState<CameraWubookInfoDto | null>(null)

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Nuova camera su OTA</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>Scegli quale camera locale creare su OTA.</Typography>
        <Autocomplete
          options={camere}
          getOptionLabel={(c) => (c.tipologiaNome ? `${c.cameraNome} · ${c.tipologiaNome}` : c.cameraNome)}
          value={selezionata}
          onChange={(_, valore) => setSelezionata(valore)}
          noOptionsText="Nessuna camera locale da creare — sono già tutte associate."
          renderInput={(params) => <TextField {...params} label="Camera locale" autoFocus />}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose}>Annulla</Button>
        <Button variant="contained" color="primary" disabled={!selezionata} onClick={() => selezionata && onSeleziona(selezionata)}>
          Continua
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function TabPianiPrezzo({ strutturaId }: { strutturaId: string }) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const piani = usePianiPrezzo(strutturaId)
  const elimina = useEliminaPianoPrezzo(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | PianoPrezzoDto>('chiuso')
  const toast = useToast()

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Piani prezzo nominati/virtuali (es. "Non rimborsabile -10%"), derivati dal piano di partenza con una variazione fissa o
        percentuale. La mappatura piano→canale si fa nel pannello dell'OTA.
      </Typography>

      {puoScrivere && (
        <Box>
          <BottoneNuovo etichetta="+ Nuovo piano" onClick={() => setDialogo('nuovo')} />
        </Box>
      )}

      {piani.isLoading && <Skeleton variant="rounded" height={180} />}

      {!piani.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(piani.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessun piano prezzo." />}
          {(piani.data ?? []).map((p) => (
            <CardElenco key={p.id}>
              <TestataCardElenco titolo={p.nome} sottotitolo={p.isVirtual ? `Virtuale (da ${p.parentId})` : 'Base'} />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Id', valore: p.id },
                  { etichetta: 'Variazione', valore: p.variazione != null ? `${p.variazione} (${p.tipoVariazione === 2 ? '%' : '€'})` : '—' },
                ]}
              />
              {p.isVirtual && puoScrivere && (
                <AzioniCardElenco>
                  <Tooltip title="Modifica">
                    <IconButton size="small" onClick={() => setDialogo(p)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Elimina">
                    <IconButton size="small" onClick={() => elimina.mutate(p.id, { onError: gestisciErrore })} disabled={elimina.isPending}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
        </Box>
      )}

      {!piani.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Id</TableCell>
                <TableCell>Nome</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Variazione</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(piani.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun piano prezzo.
                  </TableCell>
                </TableRow>
              )}
              {(piani.data ?? []).map((p) => (
                <TableRow key={p.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.id}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{p.nome}</TableCell>
                  <TableCell>{p.isVirtual ? `Virtuale (da ${p.parentId})` : 'Base'}</TableCell>
                  <TableCell>{p.variazione != null ? `${p.variazione} (${p.tipoVariazione === 2 ? '%' : '€'})` : '—'}</TableCell>
                  <TableCell align="right">
                    {p.isVirtual && puoScrivere && (
                      <>
                        <Tooltip title="Modifica">
                          <IconButton size="small" onClick={() => setDialogo(p)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="Elimina">
                          <IconButton size="small" onClick={() => elimina.mutate(p.id, { onError: gestisciErrore })} disabled={elimina.isPending}>
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && (
        <PianoPrezzoDialog strutturaId={strutturaId} piano={dialogo === 'nuovo' ? null : dialogo} piani={piani.data ?? []} onClose={() => setDialogo('chiuso')} />
      )}
    </Box>
  )
}

function TabPianiRestrizione({ strutturaId }: { strutturaId: string }) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const piani = usePianiRestrizione(strutturaId)
  const elimina = useEliminaPianoRestrizione(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | PianoRestrizioneDto>('chiuso')
  const toast = useToast()

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Piani restrizione nominati: regole di default (soggiorno min/max, chiusure arrivo/partenza) applicabili a un piano. Distinti dal
        soggiorno minimo/massimo per camera e periodo (tab Camere).
      </Typography>

      {puoScrivere && (
        <Box>
          <BottoneNuovo etichetta="+ Nuovo piano" onClick={() => setDialogo('nuovo')} />
        </Box>
      )}

      {piani.isLoading && <Skeleton variant="rounded" height={180} />}

      {!piani.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(piani.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessun piano restrizione." />}
          {(piani.data ?? []).map((p) => (
            <CardElenco key={p.id}>
              <TestataCardElenco titolo={p.nome} sottotitolo={`Id ${p.id}`} />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Min/Max', valore: p.regole ? `${p.regole.minStay ?? '—'} / ${p.regole.maxStay ?? '—'}` : '—' },
                  { etichetta: 'Chiuso', valore: p.regole?.chiuso ? 'Sì' : 'No' },
                ]}
              />
              {puoScrivere && (
                <AzioniCardElenco>
                  <Tooltip title="Modifica">
                    <IconButton size="small" onClick={() => setDialogo(p)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Elimina">
                    <IconButton size="small" onClick={() => elimina.mutate(p.id, { onError: gestisciErrore })} disabled={elimina.isPending}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
        </Box>
      )}

      {!piani.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Id</TableCell>
                <TableCell>Nome</TableCell>
                <TableCell>Min/Max</TableCell>
                <TableCell>Chiuso</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(piani.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun piano restrizione.
                  </TableCell>
                </TableRow>
              )}
              {(piani.data ?? []).map((p) => (
                <TableRow key={p.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.id}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{p.nome}</TableCell>
                  <TableCell>{p.regole ? `${p.regole.minStay ?? '—'} / ${p.regole.maxStay ?? '—'}` : '—'}</TableCell>
                  <TableCell>{p.regole?.chiuso ? 'Sì' : 'No'}</TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <>
                        <Tooltip title="Modifica">
                          <IconButton size="small" onClick={() => setDialogo(p)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="Elimina">
                          <IconButton size="small" onClick={() => elimina.mutate(p.id, { onError: gestisciErrore })} disabled={elimina.isPending}>
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && <PianoRestrizioneDialog strutturaId={strutturaId} piano={dialogo === 'nuovo' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}
    </Box>
  )
}
