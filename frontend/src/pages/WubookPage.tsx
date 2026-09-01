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
import EditCalendarIcon from '@mui/icons-material/EditCalendarOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import SettingsIcon from '@mui/icons-material/SettingsOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useTipologie } from '../api/tipologie'
import { ApiError } from '../api/client'
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
} from '../api/integrazioni'
import { aggiungiGiorni, formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { CampoData } from '../components/CampoData'
import { fontDisplay, fontMono, tokens } from '../theme'
import { ChiusureRestrizioniDialog } from '../components/ChiusureRestrizioniDialog'
import { ImpostazioniWubookCameraDialog } from '../components/ImpostazioniWubookCameraDialog'
import { PianoPrezzoDialog } from '../components/PianoPrezzoDialog'
import { PianoRestrizioneDialog } from '../components/PianoRestrizioneDialog'

type TabWubook = 'camere' | 'piani-prezzo' | 'piani-restrizione'

export function WubookPage() {
  const { strutturaId } = useStruttura()
  const config = useWubookConfig(strutturaId)
  const camere = useCamerePerAssociazione(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const [tab, setTab] = useState<TabWubook>('camere')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 1000 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Sincronizzazione con Wubook (OTA/channel manager): camere/prezzi/disponibilità/prenotazioni. Licenza gestisoft.it e attivazione si
        configurano in Impostazioni.
      </Typography>

      {config.isLoading && <Skeleton variant="rounded" height={80} />}
      {!config.isLoading && config.data && <StatoWubook dati={config.data} />}

      {!config.isLoading && config.data?.credenzialiPronte && <SincronizzazioneForm strutturaId={strutturaId!} />}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ minHeight: 0 }}>
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
          <Chip size="small" label={dati.credenzialiPronte ? 'Credenziali Wubook pronte' : 'In attesa di rinnovo'} sx={{ bgcolor: dati.credenzialiPronte ? tokens.blue600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
        </Box>
      </Box>
      {dati.ultimoErrore && <Alert severity="warning">{dati.ultimoErrore}</Alert>}
    </Box>
  )
}

function SincronizzazioneForm({ strutturaId }: { strutturaId: string }) {
  const oggi = new Date()
  const [dataInizio, setDataInizio] = useState(formatoInputData(oggi))
  const [dataFine, setDataFine] = useState(formatoInputData(aggiungiGiorni(oggi, 30)))
  const [messaggio, setMessaggio] = useState<string | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  const sincronizzaPrezzi = useSincronizzaWubookPrezzi(strutturaId)
  const sincronizzaDisponibilita = useSincronizzaWubookDisponibilita(strutturaId)
  const sincronizzaPrenotazioni = useSincronizzaWubookPrenotazioni(strutturaId)

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function esegui(azione: 'prezzi' | 'disponibilita' | 'prenotazioni') {
    setErrore(null)
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

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {messaggio && <Alert severity="success" onClose={() => setMessaggio(null)}>{messaggio}</Alert>}

      <Box sx={{ display: 'flex', gap: 2 }}>
        <CampoData label="Dal" value={dataInizio} onChange={setDataInizio} fullWidth disabled={inCorso} />
        <CampoData label="Al" value={dataFine} onChange={setDataFine} min={dataInizio || undefined} fullWidth disabled={inCorso} />
      </Box>

      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <Button variant="outlined" onClick={() => esegui('prezzi')} disabled={inCorso}>
          Sincronizza prezzi
        </Button>
        <Button variant="outlined" onClick={() => esegui('disponibilita')} disabled={inCorso}>
          Sincronizza disponibilità
        </Button>
        <Button variant="outlined" onClick={() => esegui('prenotazioni')} disabled={inCorso}>
          Sincronizza prenotazioni (pull)
        </Button>
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
  const [errore, setErrore] = useState<string | null>(null)
  const [tipologiaFiltro, setTipologiaFiltro] = useState('')
  const [dialogoChiusure, setDialogoChiusure] = useState<{ cameraId: string; cameraNome: string } | null>(null)
  const [dialogoAssocia, setDialogoAssocia] = useState<CameraWubookInfoDto | null>(null)
  const [dialogoImpostazioni, setDialogoImpostazioni] = useState<CameraWubookInfoDto | null>(null)
  const rimuovi = useRimuoviWubookCamera(strutturaId)

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function eliminaDaWubook(c: CameraWubookInfoDto) {
    if (!window.confirm(`Eliminare "${c.cameraNome}" da Wubook? La camera locale resta, solo la camera su Wubook viene rimossa.`)) return
    rimuovi.mutate(c.cameraId, { onError: gestisciErrore })
  }

  // Come otaservice.web (legacy): select Tipologia prima di tutto, poi solo le camere di quella
  // tipologia — mai la tabella piatta con tutte le camere della struttura insieme.
  const camereFiltrate = camere.filter((c) => tipologiaFiltro === '' || c.tipologiaId === tipologiaFiltro)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      <TextField select size="small" label="Tipologia" value={tipologiaFiltro} onChange={(e) => setTipologiaFiltro(e.target.value)} sx={{ minWidth: 240 }}>
        <MenuItem value="">Tutte le tipologie</MenuItem>
        {tipologie.map((t) => (
          <MenuItem key={t.id} value={t.id}>
            {t.tipologiaCamera}
          </MenuItem>
        ))}
      </TextField>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Camera</TableCell>
              <TableCell>Tipologia</TableCell>
              <TableCell>Disponibilità oggi</TableCell>
              <TableCell>Associazione Wubook</TableCell>
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
                  {!c.wubookAttiva && (
                    <>
                      <Tooltip title="Associa a una camera già esistente su Wubook">
                        <IconButton size="small" onClick={() => setDialogoAssocia(c)}>
                          <LinkIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Crea su Wubook">
                        <IconButton size="small" onClick={() => setDialogoImpostazioni(c)}>
                          <SettingsIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  )}
                  {c.wubookAttiva && (
                    <>
                      <Tooltip title="Chiusure e restrizioni per periodo">
                        <IconButton size="small" onClick={() => setDialogoChiusure({ cameraId: c.cameraId, cameraNome: c.cameraNome })}>
                          <EditCalendarIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Modifica su Wubook (codice camera, prezzo, WooDoo)">
                        <IconButton size="small" onClick={() => setDialogoImpostazioni(c)}>
                          <SettingsIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Elimina da Wubook">
                        <IconButton size="small" onClick={() => eliminaDaWubook(c)} disabled={rimuovi.isPending}>
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

      {dialogoImpostazioni && (
        <ImpostazioniWubookCameraDialog
          strutturaId={strutturaId}
          cameraId={dialogoImpostazioni.cameraId}
          cameraNome={dialogoImpostazioni.cameraNome}
          wubookAttiva={dialogoImpostazioni.wubookAttiva}
          onClose={() => setDialogoImpostazioni(null)}
        />
      )}
    </Box>
  )
}

function AssociaCameraWubookDialog({ strutturaId, camera, onClose }: { strutturaId: string; camera: CameraWubookInfoDto; onClose: () => void }) {
  const remote = useCamereRemoteWubook(strutturaId, true)
  const associa = useAssociaCameraWubook(strutturaId)
  const [selezionata, setSelezionata] = useState<CameraWubookRemoteDto | null>(null)
  const [errore, setErrore] = useState<string | null>(null)

  function conferma() {
    if (!selezionata) {
      setErrore('Seleziona una camera Wubook.')
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
      <DialogTitle>Associa "{camera.cameraNome}" a Wubook</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Scegli la camera già presente su Wubook a cui corrisponde questa camera locale — nessuna nuova camera viene creata su Wubook,
          solo l'associazione viene salvata (a differenza di "Crea su Wubook", che invece crea una camera nuova).
        </Typography>
        {remote.isLoading && <Skeleton variant="rounded" height={56} />}
        {remote.isError && <Alert severity="error">Impossibile recuperare le camere da Wubook. Verifica le credenziali in Impostazioni.</Alert>}
        {!remote.isLoading && !remote.isError && (
          <Autocomplete
            options={remote.data ?? []}
            getOptionLabel={(r) => `${r.nome} (id ${r.id})`}
            value={selezionata}
            onChange={(_, valore) => setSelezionata(valore)}
            disabled={associa.isPending}
            noOptionsText="Nessuna camera trovata su Wubook"
            renderInput={(params) => <TextField {...params} label="Camera Wubook" placeholder="Cerca per nome…" autoFocus />}
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
        <Button variant="contained" color="secondary" onClick={conferma} disabled={associa.isPending || remote.isLoading}>
          Associa
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function TabPianiPrezzo({ strutturaId }: { strutturaId: string }) {
  const piani = usePianiPrezzo(strutturaId)
  const elimina = useEliminaPianoPrezzo(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | PianoPrezzoDto>('chiuso')
  const [errore, setErrore] = useState<string | null>(null)

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Piani prezzo nominati/virtuali (es. "Non rimborsabile -10%"), derivati dal piano di partenza con una variazione fissa o
        percentuale. La mappatura piano→canale si fa nel pannello Wubook.
      </Typography>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      <Box>
        <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuovo')}>
          + Nuovo piano
        </Button>
      </Box>

      {piani.isLoading && <Skeleton variant="rounded" height={180} />}

      {!piani.isLoading && (
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
                    {p.isVirtual && (
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
  const piani = usePianiRestrizione(strutturaId)
  const elimina = useEliminaPianoRestrizione(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | PianoRestrizioneDto>('chiuso')
  const [errore, setErrore] = useState<string | null>(null)

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Piani restrizione nominati: regole di default (soggiorno min/max, chiusure arrivo/partenza) applicabili a un piano. Distinti dal
        soggiorno minimo/massimo per camera e periodo (tab Camere).
      </Typography>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      <Box>
        <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuovo')}>
          + Nuovo piano
        </Button>
      </Box>

      {piani.isLoading && <Skeleton variant="rounded" height={180} />}

      {!piani.isLoading && (
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
