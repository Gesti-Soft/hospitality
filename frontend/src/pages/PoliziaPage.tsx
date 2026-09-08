import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { ApiError } from '../api/client'
import {
  esportaSchedinaAlloggiatiWebSingola,
  esportaSchedineAlloggiatiWeb,
  useAlloggiatiWebConfig,
  useAnniAlloggiatiWeb,
  useInviaAlloggiatiWebOra,
  useSchedineAlloggiatiWeb,
} from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { usePuoScrivere } from '../permessi/usePuoScrivere'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../lib/anni'
import { useMobile } from '../lib/useMobile'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../components/CardElenco'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function PoliziaPage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoInviare = usePuoScrivere('statePoliceWrite')
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniAlloggiatiWeb(strutturaId)
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)
  const config = useAlloggiatiWebConfig(strutturaId)
  const schedine = useSchedineAlloggiatiWeb(strutturaId, anno)
  const [risultatoInvio, setRisultatoInvio] = useState<{ inviate: number; totale: number; errori: number; messaggio: string | null } | null>(null)
  const toast = useToast()

  const invia = useInviaAlloggiatiWebOra(strutturaId)

  function inviaOra() {
    setRisultatoInvio(null)
    invia.mutate(undefined, {
      onSuccess: (r) => setRisultatoInvio({ inviate: r.inviate, totale: r.totaleSchedine, errori: r.errori, messaggio: r.messaggio }),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  async function esporta() {
    if (!strutturaId) return
    try {
      await esportaSchedineAlloggiatiWeb(strutturaId, anno)
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }

  async function esportaSingola(ospiteId: string) {
    if (!strutturaId) return
    try {
      await esportaSchedinaAlloggiatiWebSingola(strutturaId, ospiteId, anno)
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 900 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero delle schedine di soggiorno alla Polizia di Stato (Alloggiati Web). Credenziali e orario di invio automatico si
        configurano in Impostazioni.
      </Typography>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Stato invii</Typography>
          {config.data && (
            <Chip
              size="small"
              label={config.data.credenzialiConfigurate ? 'Credenziali configurate' : 'Credenziali non configurate'}
              sx={{ bgcolor: config.data.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }}
            />
          )}
        </Box>

        {config.data && (
          <Box sx={{ display: 'flex', gap: 4 }}>
            <Campo etichetta="Ultimo invio" valore={config.data.ultimoInvioAtUtc ? formattatoreDataOra.format(new Date(config.data.ultimoInvioAtUtc)) : 'mai eseguito'} />
            <Campo etichetta="Schedine nell'ultimo invio" valore={String(config.data.ultimeSchedineInviate ?? '—')} />
          </Box>
        )}

        {config.data?.ultimoErrore && <Alert severity="warning">{config.data.ultimoErrore}</Alert>}

        {risultatoInvio && (
          <Alert severity={risultatoInvio.errori > 0 ? 'warning' : 'success'} onClose={() => setRisultatoInvio(null)}>
            {risultatoInvio.inviate}/{risultatoInvio.totale} schedine inviate.
            {risultatoInvio.messaggio ? ` ${risultatoInvio.messaggio}` : ''}
          </Alert>
        )}

        <Box sx={{ display: 'flex', gap: 1.5, mt: 0.5 }}>
          {puoInviare && (
            <Button variant="contained" color="primary" onClick={inviaOra} disabled={invia.isPending}>
              Invia ora
            </Button>
          )}
          <Button variant="outlined" onClick={esporta} disabled={(schedine.data ?? []).length === 0}>
            Esporta schedine del giorno
          </Button>
        </Box>
      </Box>

      <Box>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Schedine</Typography>
          <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
            {anniSelezionabili.map((a) => (
              <MenuItem key={a} value={a}>
                {a}
              </MenuItem>
            ))}
          </TextField>
        </Box>
        {schedine.isLoading && <Skeleton variant="rounded" height={220} />}

        {!schedine.isLoading && mobile && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            {(schedine.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessuna schedina per l'anno selezionato." />}
            {(schedine.data ?? []).map((s) => (
              <CardElenco key={s.ospiteId}>
                <TestataCardElenco
                  titolo={s.nomeOspite}
                  azioneDestra={
                    <Chip size="small" label={s.inviata ? 'Inviata' : 'Da inviare'} sx={{ bgcolor: s.inviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                  }
                />
                <RigaCardMeta
                  voci={[
                    { etichetta: 'Camera', valore: s.camera ?? '—' },
                    { etichetta: 'Check-in', valore: s.checkIn ? formattatoreData.format(new Date(s.checkIn)) : '—' },
                    { etichetta: 'Check-out', valore: s.checkOut ? formattatoreData.format(new Date(s.checkOut)) : '—' },
                  ]}
                />
                {!s.inviata && (
                  <AzioniCardElenco>
                    <Button size="small" variant="outlined" onClick={() => esportaSingola(s.ospiteId)}>
                      Scarica
                    </Button>
                  </AzioniCardElenco>
                )}
              </CardElenco>
            ))}
          </Box>
        )}

        {!schedine.isLoading && !mobile && (
          <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Ospite</TableCell>
                  <TableCell>Camera</TableCell>
                  <TableCell>Check-in</TableCell>
                  <TableCell>Check-out</TableCell>
                  <TableCell>Stato</TableCell>
                  <TableCell align="right">Azioni</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {(schedine.data ?? []).length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                      Nessuna schedina per l'anno selezionato.
                    </TableCell>
                  </TableRow>
                )}
                {(schedine.data ?? []).map((s) => (
                  <TableRow key={s.ospiteId} hover>
                    <TableCell sx={{ fontWeight: 700 }}>{s.nomeOspite}</TableCell>
                    <TableCell>{s.camera ?? '—'}</TableCell>
                    <TableCell sx={{ fontFamily: fontMono }}>{s.checkIn ? formattatoreData.format(new Date(s.checkIn)) : '—'}</TableCell>
                    <TableCell sx={{ fontFamily: fontMono }}>{s.checkOut ? formattatoreData.format(new Date(s.checkOut)) : '—'}</TableCell>
                    <TableCell>
                      <Chip size="small" label={s.inviata ? 'Inviata' : 'Da inviare'} sx={{ bgcolor: s.inviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                    </TableCell>
                    <TableCell align="right">
                      {!s.inviata && (
                        <Button size="small" variant="outlined" onClick={() => esportaSingola(s.ospiteId)}>
                          Scarica
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Box>
    </Box>
  )
}

function Campo({ etichetta, valore }: { etichetta: string; valore: string }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.04em' }}>{etichetta}</Typography>
      <Typography sx={{ fontFamily: fontMono, fontSize: 14, fontWeight: 600 }}>{valore}</Typography>
    </Box>
  )
}
