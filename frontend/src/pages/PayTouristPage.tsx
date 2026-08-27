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
import EditIcon from '@mui/icons-material/EditOutlined'
import DownloadIcon from '@mui/icons-material/DownloadOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useTipologie } from '../api/tipologie'
import { ApiError } from '../api/client'
import {
  esportaPayTourist,
  useAggiornaPayTouristConfig,
  useInviaPayTouristOra,
  usePayTouristConfig,
  usePayTouristStrutture,
  type PayTouristIntegrazioneDto,
  type PayTouristStrutturaDto,
} from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { PayTouristStrutturaDialog } from '../components/PayTouristStrutturaDialog'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function PayTouristPage() {
  const { strutturaId } = useStruttura()
  const config = usePayTouristConfig(strutturaId)
  const strutture = usePayTouristStrutture(strutturaId)
  const tipologie = useTipologie(strutturaId)

  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | PayTouristStrutturaDto>('chiuso')
  const [errore, setErrore] = useState<string | null>(null)
  const [risultatoInvio, setRisultatoInvio] = useState<string | null>(null)

  const invia = useInviaPayTouristOra(strutturaId)

  function inviaOra() {
    setErrore(null)
    setRisultatoInvio(null)
    invia.mutate(undefined, {
      onSuccess: (r) => setRisultatoInvio(`${r.inviate}/${r.totalePrenotazioni} prenotazioni inviate.${r.messaggio ? ` ${r.messaggio}` : ''}`),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  async function esporta(s: PayTouristStrutturaDto) {
    try {
      if (strutturaId) await esportaPayTourist(strutturaId, s.id)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 900 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero delle prenotazioni completate a PayTourist per la tassa di soggiorno. Una struttura può avere più "strutture"
        PayTourist, ognuna con il proprio identificativo e un sottoinsieme di tipologie camera.
      </Typography>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {config.isLoading && <Skeleton variant="rounded" height={140} />}
      {!config.isLoading && config.data && <ConfigForm strutturaId={strutturaId!} dati={config.data} />}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Strutture PayTourist</Typography>
        <Box sx={{ display: 'flex', gap: 1.5 }}>
          <Button variant="outlined" size="small" onClick={inviaOra} disabled={invia.isPending}>
            Invia ora tutte
          </Button>
          <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuova')} disabled={!strutturaId}>
            + Nuova struttura
          </Button>
        </Box>
      </Box>

      {risultatoInvio && (
        <Alert severity="info" onClose={() => setRisultatoInvio(null)}>
          {risultatoInvio}
        </Alert>
      )}

      {(strutture.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={200} />}

      {!strutture.isLoading && !tipologie.isLoading && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell>Id PayTourist</TableCell>
                <TableCell>Ultimo invio</TableCell>
                <TableCell>Ultimo errore</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(strutture.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna struttura PayTourist configurata.
                  </TableCell>
                </TableRow>
              )}
              {(strutture.data ?? []).map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{s.nome}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{s.idStrutturaPaytourist ?? '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono, fontSize: 12.5 }}>
                    {s.ultimoInvioAtUtc ? formattatoreDataOra.format(new Date(s.ultimoInvioAtUtc)) : 'mai eseguito'}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12, color: tokens.error600 }}>{s.ultimoErrore ?? '—'}</TableCell>
                  <TableCell align="right">
                    <Tooltip title="Esporta JSON">
                      <IconButton size="small" onClick={() => esporta(s)}>
                        <DownloadIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Modifica">
                      <IconButton size="small" onClick={() => setDialogo(s)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <PayTouristStrutturaDialog
          strutturaId={strutturaId}
          strutturaPayTourist={dialogo === 'nuova' ? null : dialogo}
          tipologie={tipologie.data ?? []}
          onClose={() => setDialogo('chiuso')}
        />
      )}
    </Box>
  )
}

function ConfigForm({ strutturaId, dati }: { strutturaId: string; dati: PayTouristIntegrazioneDto }) {
  const [token, setToken] = useState('')
  const [portaleOnlineAttivo, setPortaleOnlineAttivo] = useState(dati.portaleOnlineAttivo)
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiorna = useAggiornaPayTouristConfig(strutturaId)

  function salva() {
    setErrore(null)
    setSalvato(false)
    aggiorna.mutate(
      { token: token.trim() === '' ? null : token.trim(), portaleOnlineAttivo },
      {
        onSuccess: () => {
          setSalvato(true)
          setToken('')
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Token PayTourist</Typography>
        <Chip size="small" label={dati.tokenConfigurato ? 'Configurato' : 'Non configurato'} sx={{ bgcolor: dati.tokenConfigurato ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
      </Box>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Configurazione salvata.</Alert>}

      <TextField
        label="Token"
        type="password"
        value={token}
        onChange={(e) => setToken(e.target.value)}
        disabled={aggiorna.isPending}
        helperText={dati.tokenConfigurato ? "Già salvato: lasciarlo vuoto e salvare lo AZZERA" : ' '}
      />

      <FormControlLabel
        control={<Checkbox checked={portaleOnlineAttivo} onChange={(e) => setPortaleOnlineAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
        label="Filtra per portale online (scarta le prenotazioni di canali senza un portale PayTourist corrispondente)"
      />

      <Box>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva
        </Button>
      </Box>
    </Box>
  )
}
