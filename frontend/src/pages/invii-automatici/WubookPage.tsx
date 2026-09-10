import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
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
import LinkOffIcon from '@mui/icons-material/LinkOffOutlined'
import EditCalendarIcon from '@mui/icons-material/EditCalendarOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import SettingsIcon from '@mui/icons-material/SettingsOutlined'
import MoreVertIcon from '@mui/icons-material/MoreVertOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useAggiornaTipologia, useTipologie, type TipologiaCameraDto, type TipologiaCameraRequest } from '../../api/tipologie'
import { useCamere, type CameraDto } from '../../api/camere'
import { ApiError } from '../../api/client'
import {
  useAssociaTipologiaWubook,
  useTipologiePerAssociazione,
  useCamereRemoteWubook,
  useEliminaPianoPrezzo,
  useEliminaPianoRestrizione,
  usePianiPrezzo,
  usePianiRestrizione,
  useRimuoviWubookTipologia,
  useRimuoviWubookTipologiaRemota,
  useSincronizzaWubookTipologia,
  useSincronizzaWubookDisponibilita,
  useSincronizzaWubookPrenotazioni,
  useSincronizzaWubookPrezzi,
  useWubookConfig,
  type TipologiaWubookInfoDto,
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
import { PianoPrezzoDialog } from '../../components/PianoPrezzoDialog'
import { PianoRestrizioneDialog } from '../../components/PianoRestrizioneDialog'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

type TabWubook = 'camere' | 'piani-prezzo' | 'piani-restrizione'

export function WubookPage() {
  const { strutturaId } = useStruttura()
  const config = useWubookConfig(strutturaId)
  const credenzialiPronte = !!config.data?.credenzialiPronte
  const tipologiePerAssociazione = useTipologiePerAssociazione(strutturaId)
  // Come otaservice.web (legacy): l'elenco principale della tab Camere sono le camere già presenti
  // sull'OTA (fetch_rooms), non quelle locali — caricato qui, non solo dentro un dialog, perché ora
  // è la fonte primaria della tabella. L'associazione vive però sulla Tipologia (il pool di camere
  // reali identiche), non più sulla singola camera.
  const remote = useCamereRemoteWubook(strutturaId, credenzialiPronte)
  const tipologieComplete = useTipologie(strutturaId)
  const [tab, setTab] = useState<TabWubook>('camere')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 1000 }}>

      {config.isLoading && <Skeleton variant="rounded" height={80} />}
      {!config.isLoading && config.data && <StatoWubook dati={config.data} />}

      {!config.isLoading && credenzialiPronte && <SincronizzazioneForm strutturaId={strutturaId!} />}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ minHeight: 0 }}>
        <Tab label="Camere" value="camere" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Piani prezzo" value="piani-prezzo" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Piani restrizione" value="piani-restrizione" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
      </Tabs>

      {tab === 'camere' && (
        <Box>
          {tipologiePerAssociazione.isLoading && <Skeleton variant="rounded" height={180} />}
          {!tipologiePerAssociazione.isLoading && (
            <TabellaCamere
              strutturaId={strutturaId!}
              tipologiePerAssociazione={tipologiePerAssociazione.data ?? []}
              tipologieComplete={tipologieComplete.data ?? []}
              remote={remote}
              credenzialiPronte={credenzialiPronte}
            />
          )}
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
  tipologiePerAssociazione,
  tipologieComplete,
  remote,
  credenzialiPronte,
}: {
  strutturaId: string
  tipologiePerAssociazione: TipologiaWubookInfoDto[]
  tipologieComplete: TipologiaCameraDto[]
  remote: ReturnType<typeof useCamereRemoteWubook>
  credenzialiPronte: boolean
}) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const camereComplete = useCamere(strutturaId)
  const [dialogoChiusure, setDialogoChiusure] = useState<{ cameraId: string; cameraNome: string } | null>(null)
  const [scegliCameraChiusura, setScegliCameraChiusura] = useState<CameraDto[] | null>(null)
  const [dialogoTipologia, setDialogoTipologia] = useState<'chiuso' | 'nuova' | CameraWubookRemoteDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<{ remoto: CameraWubookRemoteDto | null; locale: TipologiaWubookInfoDto | null } | null>(null)
  const [daDisassociare, setDaDisassociare] = useState<TipologiaWubookInfoDto | null>(null)
  const rimuovi = useRimuoviWubookTipologia(strutturaId)
  const rimuoviRemota = useRimuoviWubookTipologiaRemota(strutturaId)
  const associa = useAssociaTipologiaWubook(strutturaId)
  const toast = useToast()

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function confermaEliminaDaOta() {
    if (!daEliminare) return
    if (daEliminare.locale) {
      rimuovi.mutate(daEliminare.locale.tipologiaId, { onSuccess: () => setDaEliminare(null), onError: gestisciErrore })
    } else if (daEliminare.remoto) {
      rimuoviRemota.mutate(daEliminare.remoto.id, { onSuccess: () => setDaEliminare(null), onError: gestisciErrore })
    }
  }

  // Disassocia = scollega la Tipologia da quella su OTA senza toccare OTA (nessun del_room): la
  // camera resta lì intatta, così si può riassociarla in seguito riaprendo "Modifica" sulla stessa
  // riga OTA (o su un'altra) — a differenza di "Elimina da OTA", che invece la cancella davvero.
  function confermaDisassocia() {
    if (!daDisassociare) return
    associa.mutate(
      { tipologiaId: daDisassociare.tipologiaId, idCameraWubook: null },
      { onSuccess: () => setDaDisassociare(null), onError: gestisciErrore },
    )
  }

  // "Chiudi periodo" agisce su una camera reale specifica del pool (manutenzione di quell'unità),
  // mai sulla Tipologia intera: se il pool ha una sola camera si salta dritti al dialog (stesso
  // comportamento di prima, zero click in più per il caso non-pool), altrimenti si chiede prima
  // quale delle camere reali della Tipologia.
  function apriChiusure(locale: TipologiaWubookInfoDto) {
    const camereDelPool = camereComplete.data?.filter((c) => c.tipologiaId === locale.tipologiaId) ?? []
    if (camereDelPool.length === 1) {
      setDialogoChiusure({ cameraId: camereDelPool[0].id, cameraNome: camereDelPool[0].nome })
    } else if (camereDelPool.length > 1) {
      setScegliCameraChiusura(camereDelPool)
    }
  }

  // Tipologie non ancora presenti su OTA — il bacino da cui pescare per "Tipologia esistente" nel
  // dialog Nuova/Modifica, e con almeno una camera reale collegata (altrimenti non c'è nulla da
  // sincronizzare: lo stesso vincolo già applicato lato backend).
  const tipologieDisponibili = tipologiePerAssociazione.filter((t) => !t.wubookAttiva && t.camereCollegate > 0)

  // Come otaservice.web: l'elenco principale della tabella sono le camere sull'OTA (fetch_rooms), non
  // quelle locali — flat, senza filtro Tipologia (il legacy non ne ha uno). Ogni riga OTA corrisponde
  // però ora a una Tipologia (il pool), non più a una singola camera.
  type RigaCameraOta = { remoto: CameraWubookRemoteDto | null; locale: TipologiaWubookInfoDto | null }
  const righe: RigaCameraOta[] = remote.data
    ? remote.data.map((r) => ({ remoto: r, locale: tipologiePerAssociazione.find((t) => t.wubookAttiva && t.idCameraWubook === r.id) ?? null }))
    : // OTA momentaneamente non raggiungibile: fallback alle sole tipologie già associate in precedenza,
      // per non bloccare chi deve solo aprire le chiusure/disassociare/eliminare.
      tipologiePerAssociazione.filter((t) => t.wubookAttiva).map((t) => ({ remoto: null, locale: t }))

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 1.5 }}>
        {puoScrivere && <BottoneNuovo etichetta="+ Nuova camera" onClick={() => setDialogoTipologia('nuova')} disabilitato={tipologieDisponibili.length === 0} />}
      </Box>

      {!credenzialiPronte && <Alert severity="info">Configura le credenziali OTA in Impostazioni per vedere e collegare le camere.</Alert>}

      {credenzialiPronte && remote.isError && (
        <Alert severity="warning">
          Impossibile recuperare l'elenco camere dall'OTA in questo momento — mostrate solo le camere già associate in precedenza.
        </Alert>
      )}

      {credenzialiPronte && remote.isLoading && <Skeleton variant="rounded" height={180} />}

      {credenzialiPronte && !remote.isLoading && (
        <>
          {mobile && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {righe.length === 0 && <MessaggioVuotoElenco messaggio="Nessuna camera trovata sull'OTA." />}
              {righe.map((r) => (
                <CardElenco key={r.remoto?.id ?? r.locale!.tipologiaId} coloreAccento={r.locale ? tokens.ok600 : tokens.wait600}>
                  <TestataCardElenco
                    titolo={r.remoto?.nome ?? r.locale!.tipologiaNome}
                    sottotitolo={r.remoto ? `ID ${r.remoto.id}` : undefined}
                  />
                  <RigaCardMeta
                    voci={[
                      { etichetta: 'Posti', valore: r.remoto?.occupancy ?? '—' },
                      { etichetta: 'Prezzo', valore: r.remoto ? `€${r.remoto.prezzo.toFixed(2)}` : '—' },
                      { etichetta: 'Disponibilità', valore: r.remoto?.disponibilita ?? '—' },
                      { etichetta: 'Camere collegate', valore: r.locale?.camereCollegate ?? '—' },
                    ]}
                  />
                  <AzioniCardElenco>
                    <MenuAzioniCamera
                      riga={r}
                      puoScrivere={puoScrivere}
                      onModifica={() => r.remoto && setDialogoTipologia(r.remoto)}
                      onChiusure={() => r.locale && apriChiusure(r.locale)}
                      onDisassocia={() => r.locale && setDaDisassociare(r.locale)}
                      onElimina={() => setDaEliminare(r)}
                      disassociaInCorso={associa.isPending}
                      eliminaInCorso={rimuovi.isPending || rimuoviRemota.isPending}
                    />
                  </AzioniCardElenco>
                </CardElenco>
              ))}
            </Box>
          )}

          {!mobile && (
            <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>ID</TableCell>
                    <TableCell>Nome</TableCell>
                    <TableCell>Posti</TableCell>
                    <TableCell>Prezzo</TableCell>
                    <TableCell>Disponibilità</TableCell>
                    <TableCell>Camere collegate</TableCell>
                    <TableCell align="right">Azioni</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {righe.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={7} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                        Nessuna camera trovata sull'OTA.
                      </TableCell>
                    </TableRow>
                  )}
                  {righe.map((r) => (
                    <TableRow key={r.remoto?.id ?? r.locale!.tipologiaId} hover>
                      <TableCell sx={{ fontFamily: fontMono, color: tokens.textTertiary }}>{r.remoto?.id ?? '—'}</TableCell>
                      <TableCell sx={{ fontWeight: 700 }}>{r.remoto?.nome ?? r.locale!.tipologiaNome}</TableCell>
                      <TableCell>{r.remoto?.occupancy ?? '—'}</TableCell>
                      <TableCell>{r.remoto ? `€${r.remoto.prezzo.toFixed(2)}` : '—'}</TableCell>
                      <TableCell>{r.remoto?.disponibilita ?? '—'}</TableCell>
                      <TableCell>{r.locale?.camereCollegate ?? '—'}</TableCell>
                      <TableCell align="right">
                        <MenuAzioniCamera
                          riga={r}
                          puoScrivere={puoScrivere}
                          onModifica={() => r.remoto && setDialogoTipologia(r.remoto)}
                          onChiusure={() => r.locale && apriChiusure(r.locale)}
                          onDisassocia={() => r.locale && setDaDisassociare(r.locale)}
                          onElimina={() => setDaEliminare(r)}
                          disassociaInCorso={associa.isPending}
                          eliminaInCorso={rimuovi.isPending || rimuoviRemota.isPending}
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </>
      )}

      {scegliCameraChiusura && (
        <SceltaCameraChiusuraDialog
          camere={scegliCameraChiusura}
          onSeleziona={(c) => {
            setScegliCameraChiusura(null)
            setDialogoChiusure({ cameraId: c.id, cameraNome: c.nome })
          }}
          onClose={() => setScegliCameraChiusura(null)}
        />
      )}

      {dialogoChiusure && (
        <ChiusureRestrizioniDialog
          strutturaId={strutturaId}
          cameraId={dialogoChiusure.cameraId}
          cameraNome={dialogoChiusure.cameraNome}
          onClose={() => setDialogoChiusure(null)}
        />
      )}

      {dialogoTipologia !== 'chiuso' && (
        <TipologiaOtaDialog
          strutturaId={strutturaId}
          remoto={dialogoTipologia === 'nuova' ? null : dialogoTipologia}
          tipologiePerAssociazione={tipologiePerAssociazione}
          tipologieComplete={tipologieComplete}
          onClose={() => setDialogoTipologia('chiuso')}
        />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare da OTA"
          messaggio={`Eliminare "${daEliminare.remoto?.nome ?? daEliminare.locale?.tipologiaNome}" da OTA? ${daEliminare.locale ? 'Le camere locali restano, solo la camera sull\'OTA viene rimossa.' : 'Non è associata a nessuna tipologia locale.'}`}
          inCorso={rimuovi.isPending || rimuoviRemota.isPending}
          onConferma={confermaEliminaDaOta}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}

      {daDisassociare && (
        <ConfirmDialog
          titolo="Disassociare da OTA"
          messaggio={`Disassociare "${daDisassociare.tipologiaNome}" da OTA? La camera resta invariata sull'OTA (non viene eliminata) — potrai riassociarla in seguito riaprendo "Modifica" su questa riga.`}
          inCorso={associa.isPending}
          onConferma={confermaDisassocia}
          onAnnulla={() => setDaDisassociare(null)}
        />
      )}
    </Box>
  )
}

/** Azioni per riga camera raccolte in un menu a tendina (icona "···") invece di una fila di icone. */
function MenuAzioniCamera({
  riga,
  puoScrivere,
  onModifica,
  onChiusure,
  onDisassocia,
  onElimina,
  disassociaInCorso,
  eliminaInCorso,
}: {
  riga: { remoto: CameraWubookRemoteDto | null; locale: TipologiaWubookInfoDto | null }
  puoScrivere: boolean
  onModifica: () => void
  onChiusure: () => void
  onDisassocia: () => void
  onElimina: () => void
  disassociaInCorso: boolean
  eliminaInCorso: boolean
}) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const associata = riga.locale !== null

  if (!puoScrivere && !associata) return null

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
        {puoScrivere && riga.remoto && (
          <MenuItem onClick={() => esegui(onModifica)}>
            <ListItemIcon>
              <SettingsIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Modifica</ListItemText>
          </MenuItem>
        )}
        {associata && (
          <MenuItem onClick={() => esegui(onChiusure)}>
            <ListItemIcon>
              <EditCalendarIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Chiudi periodo / Soggiorno min-max</ListItemText>
          </MenuItem>
        )}
        {associata && puoScrivere && (
          <MenuItem onClick={() => esegui(onDisassocia)} disabled={disassociaInCorso}>
            <ListItemIcon>
              <LinkOffIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Disassocia</ListItemText>
          </MenuItem>
        )}
        {puoScrivere && (
          <MenuItem onClick={() => esegui(onElimina)} disabled={eliminaInCorso}>
            <ListItemIcon>
              <DeleteIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Elimina</ListItemText>
          </MenuItem>
        )}
      </Menu>
    </>
  )
}

/** Quando il pool ha più di una camera reale, chiede quale chiudere per periodo — nessuna ambiguità quando ce n'è solo una (v. apriChiusure). */
function SceltaCameraChiusuraDialog({ camere, onSeleziona, onClose }: { camere: CameraDto[]; onSeleziona: (c: CameraDto) => void; onClose: () => void }) {
  const [selezionata, setSelezionata] = useState<CameraDto | null>(null)

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Quale camera del pool?</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Questa tipologia ha più camere reali collegate: scegli quella da chiudere per periodo o su cui impostare un soggiorno minimo/massimo.
        </Typography>
        <Autocomplete
          options={camere}
          getOptionLabel={(c) => c.nome}
          value={selezionata}
          onChange={(_, valore) => setSelezionata(valore)}
          renderInput={(params) => <TextField {...params} label="Camera" autoFocus />}
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

function codiceDedottoDaNome(nome: string): string {
  const alfanumerico = nome.replace(/[^a-zA-Z0-9]/g, '').toUpperCase()
  if (alfanumerico.length === 0) return 'ROOM'
  return alfanumerico.length <= 4 ? alfanumerico.padEnd(4, 'X') : alfanumerico.slice(0, 4)
}

/**
 * Un solo dialog "Nuova/Modifica camera" per creare, associare e modificare — il soggetto
 * dell'associazione OTA è la Tipologia (il pool di camere reali identiche), non più una singola
 * camera: niente più "Posti letto"/"Disponibilità iniziale" da digitare a mano, sono sempre dedotti
 * dalle camere reali collegate. `remoto` null = creazione (nessuna camera OTA ancora esistente);
 * valorizzato = modifica di una camera OTA già esistente (associata o meno a una Tipologia locale).
 */
function TipologiaOtaDialog({
  strutturaId,
  remoto,
  tipologiePerAssociazione,
  tipologieComplete,
  onClose,
}: {
  strutturaId: string
  remoto: CameraWubookRemoteDto | null
  tipologiePerAssociazione: TipologiaWubookInfoDto[]
  tipologieComplete: TipologiaCameraDto[]
  onClose: () => void
}) {
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{remoto ? `Camere - Modifica camera #${remoto.id}` : 'Camere - Nuova camera'}</DialogTitle>
      <TipologiaOtaForm
        strutturaId={strutturaId}
        remoto={remoto}
        tipologiePerAssociazione={tipologiePerAssociazione}
        tipologieComplete={tipologieComplete}
        onClose={onClose}
      />
    </Dialog>
  )
}

function TipologiaOtaForm({
  strutturaId,
  remoto,
  tipologiePerAssociazione,
  tipologieComplete,
  onClose,
}: {
  strutturaId: string
  remoto: CameraWubookRemoteDto | null
  tipologiePerAssociazione: TipologiaWubookInfoDto[]
  tipologieComplete: TipologiaCameraDto[]
  onClose: () => void
}) {
  const aggiorna = useAggiornaTipologia(strutturaId)
  const associa = useAssociaTipologiaWubook(strutturaId)
  const sincronizza = useSincronizzaWubookTipologia(strutturaId)

  // La Tipologia già associata a questa riga OTA (se esiste) — quella da rilasciare se l'operatore
  // ne sceglie un'altra dal combo "Tipologia esistente".
  const tipologiaAttuale = remoto ? tipologiePerAssociazione.find((t) => t.wubookAttiva && t.idCameraWubook === remoto.id) ?? null : null
  const tipologiaAttualeCompleta = tipologiaAttuale ? tipologieComplete.find((t) => t.id === tipologiaAttuale.tipologiaId) ?? null : null

  // Opzioni del combo: le Tipologie non ancora su OTA (con almeno una camera reale), più quella
  // eventualmente già associata a QUESTA riga (altrimenti sparirebbe dalle opzioni non appena la si osserva).
  const opzioniCombo = tipologiePerAssociazione.filter(
    (t) => (!t.wubookAttiva && t.camereCollegate > 0) || t.tipologiaId === tipologiaAttuale?.tipologiaId,
  )

  const [selezionataId, setSelezionataId] = useState<string | null>(tipologiaAttuale?.tipologiaId ?? null)
  const [nome, setNome] = useState(remoto?.nome ?? '')
  const [codice, setCodice] = useState(remoto?.shortName ?? (remoto ? codiceDedottoDaNome(remoto.nome) : ''))
  const [prezzo, setPrezzo] = useState(remoto ? String(remoto.prezzo) : '')
  const [woodoo, setWoodoo] = useState(tipologiaAttualeCompleta?.wubookSoloWoodoo ?? false)
  const [errore, setErrore] = useState<string | null>(null)

  const inCorso = aggiorna.isPending || associa.isPending || sincronizza.isPending
  const opzioneSelezionata = opzioniCombo.find((t) => t.tipologiaId === selezionataId) ?? null

  // Come il legacy: scegliere una Tipologia precompila gli altri campi SOLO in creazione (mai in
  // modifica, dove i campi mostrano già il valore reale della camera OTA/dell'associazione).
  function seleziona(t: TipologiaWubookInfoDto | null) {
    setSelezionataId(t?.tipologiaId ?? null)
    if (!remoto && t) {
      const completa = tipologieComplete.find((x) => x.id === t.tipologiaId)
      setNome(t.tipologiaNome)
      setCodice(completa?.codiceCameraWubook ?? codiceDedottoDaNome(t.tipologiaNome))
      setPrezzo(completa?.prezzoDefault != null ? String(completa.prezzoDefault) : '')
      setWoodoo(completa?.wubookSoloWoodoo ?? false)
    }
  }

  function salva() {
    if (!selezionataId) {
      setErrore('Seleziona una tipologia esistente.')
      return
    }
    if (nome.trim() === '') {
      setErrore('Il nome è obbligatorio.')
      return
    }
    setErrore(null)

    const selezionata = tipologieComplete.find((t) => t.id === selezionataId)
    if (!selezionata) {
      setErrore('Tipologia non trovata.')
      return
    }

    const request: TipologiaCameraRequest = {
      tipologiaCamera: nome.trim(),
      prezzoDefault: prezzo.trim() === '' ? null : Number(prezzo),
      numeroImplementoPersona: selezionata.numeroImplementoPersona,
      implemento: selezionata.implemento,
      spesePulizia: selezionata.spesePulizia,
      animali: selezionata.animali,
      cauzione: selezionata.cauzione,
      codiceCameraWubook: codice.trim() === '' ? null : codice.trim(),
      wubookSoloWoodoo: woodoo,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    const giaPuntaQui = remoto !== null && selezionata.idCameraWubook === remoto.id && selezionata.wubookAttiva

    aggiorna.mutate(
      { tipologiaId: selezionataId, request },
      {
        onSuccess: () => {
          if (remoto && !giaPuntaQui) {
            associa.mutate(
              { tipologiaId: selezionataId, idCameraWubook: remoto.id },
              {
                onSuccess: () => {
                  const push = () => sincronizza.mutate(selezionataId, { onSuccess: onClose, onError })
                  if (tipologiaAttuale && tipologiaAttuale.tipologiaId !== selezionataId) {
                    associa.mutate({ tipologiaId: tipologiaAttuale.tipologiaId, idCameraWubook: null }, { onSuccess: push, onError: push })
                  } else {
                    push()
                  }
                },
                onError,
              },
            )
          } else {
            sincronizza.mutate(selezionataId, { onSuccess: onClose, onError })
          }
        },
        onError,
      },
    )
  }

  return (
    <>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Autocomplete
          options={opzioniCombo}
          getOptionLabel={(t) => t.tipologiaNome}
          value={opzioneSelezionata}
          onChange={(_, valore) => seleziona(valore)}
          disabled={inCorso}
          noOptionsText="Nessuna tipologia disponibile — creane una prima nella pagina Tipologie."
          renderInput={(params) => <TextField {...params} label="Tipologia esistente" placeholder="Seleziona tipologia…" autoFocus />}
        />
        {opzioneSelezionata && (
          <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
            {opzioneSelezionata.camereCollegate} camera/e reale/i collegata/e — la quantità inviata a OTA segue sempre questo numero.
          </Typography>
        )}
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} disabled={inCorso} />
          <TextField
            label="Codice Camera (max 4)"
            value={codice}
            onChange={(e) => setCodice(e.target.value.toUpperCase().slice(0, 4))}
            disabled={inCorso}
            slotProps={{ htmlInput: { style: { fontFamily: fontMono } } }}
          />
        </Box>
        <TextField label="Prezzo Default" type="number" value={prezzo} onChange={(e) => setPrezzo(e.target.value)} disabled={inCorso} slotProps={{ htmlInput: { min: 0 } }} />
        <FormControlLabel control={<Checkbox checked={woodoo} onChange={(e) => setWoodoo(e.target.checked)} disabled={inCorso} />} label="WooDoo CM only" />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {inCorso ? 'Salvataggio…' : 'Salva'}
        </Button>
      </DialogActions>
    </>
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
