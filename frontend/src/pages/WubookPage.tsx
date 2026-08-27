import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import SyncIcon from '@mui/icons-material/SyncOutlined'
import LinkOffIcon from '@mui/icons-material/LinkOffOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useCamere } from '../api/camere'
import { ApiError } from '../api/client'
import {
  useAggiornaWubookConfig,
  useRimuoviWubookCamera,
  useRinnovaWubookCredenziali,
  useSincronizzaWubookCamera,
  useSincronizzaWubookDisponibilita,
  useSincronizzaWubookPrenotazioni,
  useSincronizzaWubookPrezzi,
  useWubookConfig,
  type WubookIntegrazioneDto,
} from '../api/integrazioni'
import { aggiungiGiorni, formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { fontDisplay, fontMono, tokens } from '../theme'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function WubookPage() {
  const { strutturaId } = useStruttura()
  const config = useWubookConfig(strutturaId)
  const camere = useCamere(strutturaId)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 900 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Sincronizzazione con Wubook (OTA/channel manager): licenza gestisoft.it, poi camere/prezzi/disponibilità/prenotazioni.
      </Typography>

      {config.isLoading && <Skeleton variant="rounded" height={260} />}
      {!config.isLoading && config.data && <ConfigForm strutturaId={strutturaId!} dati={config.data} />}

      {!config.isLoading && config.data?.credenzialiPronte && <SincronizzazioneForm strutturaId={strutturaId!} />}

      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15, mb: 1.5 }}>Camere</Typography>
        {camere.isLoading && <Skeleton variant="rounded" height={180} />}
        {!camere.isLoading && <TabellaCamere strutturaId={strutturaId!} camere={camere.data ?? []} />}
      </Box>
    </Box>
  )
}

function ConfigForm({ strutturaId, dati }: { strutturaId: string; dati: WubookIntegrazioneDto }) {
  const [attivo, setAttivo] = useState(dati.attivo)
  const [gestisoftUsername, setGestisoftUsername] = useState(dati.gestisoftUsername ?? '')
  const [gestisoftToken, setGestisoftToken] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiorna = useAggiornaWubookConfig(strutturaId)
  const rinnova = useRinnovaWubookCredenziali(strutturaId)

  function salva() {
    setErrore(null)
    setSalvato(false)
    aggiorna.mutate(
      { attivo, gestisoftUsername: gestisoftUsername.trim() === '' ? null : gestisoftUsername.trim(), gestisoftToken: gestisoftToken.trim() === '' ? null : gestisoftToken.trim() },
      {
        onSuccess: () => {
          setSalvato(true)
          setGestisoftToken('')
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  function rinnovaOra() {
    setErrore(null)
    rinnova.mutate(undefined, { onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') })
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Licenza gestisoft.it</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Chip size="small" label={dati.licenzaConfigurata ? 'Licenza configurata' : 'Licenza non configurata'} sx={{ bgcolor: dati.licenzaConfigurata ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
          <Chip size="small" label={dati.credenzialiPronte ? 'Credenziali Wubook pronte' : 'In attesa di rinnovo'} sx={{ bgcolor: dati.credenzialiPronte ? tokens.blue600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
        </Box>
      </Box>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Configurazione salvata.</Alert>}
      {dati.ultimoErrore && <Alert severity="warning">{dati.ultimoErrore}</Alert>}

      <FormControlLabel
        control={<Checkbox checked={attivo} onChange={(e) => setAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
        label="Sincronizzazione Wubook attiva per questa struttura"
      />

      <Box sx={{ display: 'flex', gap: 2 }}>
        <TextField label="Utente gestisoft.it" value={gestisoftUsername} onChange={(e) => setGestisoftUsername(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField
          label="Token gestisoft.it"
          type="password"
          value={gestisoftToken}
          onChange={(e) => setGestisoftToken(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          helperText={dati.licenzaConfigurata ? "Già salvato: lasciarlo vuoto e salvare lo AZZERA" : ' '}
        />
      </Box>

      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Cache aggiornata: {dati.cacheAggiornataAtUtc ? formattatoreDataOra.format(new Date(dati.cacheAggiornataAtUtc)) : 'mai'}
      </Typography>

      <Box sx={{ display: 'flex', gap: 1.5 }}>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva
        </Button>
        <Button variant="outlined" onClick={rinnovaOra} disabled={rinnova.isPending || !dati.licenzaConfigurata}>
          Rinnova credenziali ora
        </Button>
      </Box>
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
      sincronizzaDisponibilita.mutate(periodo, { onSuccess: () => setMessaggio('Disponibilità sincronizzata.'), onError: gestisciErrore })
    } else {
      sincronizzaPrenotazioni.mutate(undefined, {
        onSuccess: (r) => setMessaggio(`Prenotazioni: ${r.importate} importate, ${r.aggiornate} aggiornate, ${r.annullate} annullate.${r.errori.length > 0 ? ` Errori: ${r.errori.join('; ')}` : ''}`),
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
        <TextField label="Dal" type="date" value={dataInizio} onChange={(e) => setDataInizio(e.target.value)} fullWidth slotProps={{ inputLabel: { shrink: true } }} disabled={inCorso} />
        <TextField label="Al" type="date" value={dataFine} onChange={(e) => setDataFine(e.target.value)} fullWidth slotProps={{ inputLabel: { shrink: true } }} disabled={inCorso} />
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

function TabellaCamere({ strutturaId, camere }: { strutturaId: string; camere: ReturnType<typeof useCamere>['data'] }) {
  const [errore, setErrore] = useState<string | null>(null)
  const sincronizza = useSincronizzaWubookCamera(strutturaId)
  const rimuovi = useRimuoviWubookCamera(strutturaId)

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}
      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Camera</TableCell>
              <TableCell>Stato Wubook</TableCell>
              <TableCell>Id camera Wubook</TableCell>
              <TableCell align="right">Azioni</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {(camere ?? []).length === 0 && (
              <TableRow>
                <TableCell colSpan={4} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                  Nessuna camera configurata.
                </TableCell>
              </TableRow>
            )}
            {(camere ?? []).map((c) => (
              <TableRow key={c.id} hover>
                <TableCell sx={{ fontWeight: 700 }}>{c.nome}</TableCell>
                <TableCell>
                  <Chip size="small" label={c.wubookAttiva ? 'Sincronizzata' : 'Non sincronizzata'} sx={{ bgcolor: c.wubookAttiva ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
                </TableCell>
                <TableCell sx={{ fontFamily: fontMono }}>{c.idCameraWubook ?? '—'}</TableCell>
                <TableCell align="right">
                  <Tooltip title="Sincronizza su Wubook">
                    <IconButton size="small" onClick={() => sincronizza.mutate(c.id, { onError: gestisciErrore })} disabled={sincronizza.isPending}>
                      <SyncIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {c.wubookAttiva && (
                    <Tooltip title="Rimuovi da Wubook">
                      <IconButton size="small" onClick={() => rimuovi.mutate(c.id, { onError: gestisciErrore })} disabled={rimuovi.isPending}>
                        <LinkOffIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </Box>
  )
}
