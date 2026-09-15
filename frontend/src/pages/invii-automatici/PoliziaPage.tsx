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
import { useStruttura } from '../../struttura/StrutturaContext'
import { ApiError } from '../../api/client'
import {
  esportaSchedinaAlloggiatiWebSingola,
  esportaSchedineAlloggiatiWeb,
  useAlloggiatiWebConfig,
  useAnniAlloggiatiWeb,
  useInviaAlloggiatiWebOra,
  useInviaSchedinaAlloggiatiWebSingola,
  useSchedineAlloggiatiWeb,
  type SchedinaAlloggiatiWebDto,
} from '../../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

/**
 * Quattro stati, non due. Una schedina non ancora inviata può aver superato il termine di legge
 * (24 ore dall'arrivo, 6 per i soggiorni sotto le 24 ore), e allora il portale la rifiuterebbe: va
 * registrata a mano, e l'etichetta deve dirlo invece di lasciarla indistinguibile dalle altre in
 * attesa. Oppure può avere dati incompleti o non riconosciuti dall'anagrafica della PA: anche
 * quella verrebbe rifiutata, ma qui c'è ancora tempo per correggerla — ed è il motivo per cui si
 * mostra adesso, con l'elenco di cosa manca, invece di lasciarla scoprire dal rifiuto del portale
 * quando il termine è ormai passato.
 */
function statoSchedina(s: SchedinaAlloggiatiWebDto) {
  if (s.inviata) return { etichetta: 'Inviata', colore: tokens.ok600 }
  if (!s.inTermine) return { etichetta: 'Fuori termine', colore: tokens.error600 }
  if (s.motiviNonInviabile.length > 0) return { etichetta: 'Da correggere', colore: tokens.orange700 }
  return { etichetta: 'Da inviare', colore: tokens.wait600 }
}

/// Inviabile davvero: in termine e senza dati da correggere.
const inviabile = (s: SchedinaAlloggiatiWebDto) => s.inTermine && s.motiviNonInviabile.length === 0

function terminePerSchedina(s: SchedinaAlloggiatiWebDto) {
  if (s.inviata || !s.scadenzaInvioUtc) return '—'
  const scadenza = formattatoreDataOra.format(new Date(s.scadenzaInvioUtc))
  return s.inTermine ? `entro ${scadenza}` : `scaduto il ${scadenza}`
}

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
  const inviaSingola = useInviaSchedinaAlloggiatiWebSingola(strutturaId)

  function inviaUnaSchedina(ospiteId: string, nomeOspite: string) {
    inviaSingola.mutate(ospiteId, {
      onSuccess: (r) =>
        r.inviate > 0
          ? toast.successo(`Schedina di ${nomeOspite} inviata.`)
          : toast.errore(r.messaggio ?? 'Invio non riuscito.'),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Invio non riuscito.'),
    })
  }

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
                    <Chip size="small" label={statoSchedina(s).etichetta} sx={{ bgcolor: statoSchedina(s).colore, color: '#fff', fontWeight: 700 }} />
                  }
                />
                <RigaCardMeta
                  voci={[
                    { etichetta: 'Camera', valore: s.camera ?? '—' },
                    { etichetta: 'Check-in', valore: s.checkIn ? formattatoreData.format(new Date(s.checkIn)) : '—' },
                    { etichetta: 'Check-out', valore: s.checkOut ? formattatoreData.format(new Date(s.checkOut)) : '—' },
                    { etichetta: s.soggiornoBreve ? 'Termine (6 ore)' : 'Termine', valore: terminePerSchedina(s) },
                  ]}
                />
                {!s.inviata && s.motiviNonInviabile.length > 0 && (
                  <Box sx={{ fontSize: 12, color: tokens.orange700, mt: 0.5 }}>{s.motiviNonInviabile.join('; ')}</Box>
                )}
                {!s.inviata && (
                  <AzioniCardElenco>
                    {inviabile(s) && puoInviare && (
                      <Button
                        size="small"
                        variant="contained"
                        disabled={inviaSingola.isPending}
                        onClick={() => inviaUnaSchedina(s.ospiteId, s.nomeOspite)}
                      >
                        Invia
                      </Button>
                    )}
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
                  <TableCell>Termine invio</TableCell>
                  <TableCell>Stato</TableCell>
                  <TableCell align="right">Azioni</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {(schedine.data ?? []).length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
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
                    <TableCell sx={{ fontFamily: fontMono, fontSize: 12.5, color: s.inviata || s.inTermine ? tokens.textSecondary : tokens.error600 }}>
                      {terminePerSchedina(s)}
                      {s.soggiornoBreve && !s.inviata && (
                        <Chip size="small" label="6 ore" sx={{ ml: 0.75, height: 18, fontSize: 10.5, fontWeight: 700, bgcolor: tokens.orange100, color: tokens.orange700 }} />
                      )}
                    </TableCell>
                    <TableCell>
                      <Chip size="small" label={statoSchedina(s).etichetta} sx={{ bgcolor: statoSchedina(s).colore, color: '#fff', fontWeight: 700 }} />
                      {!s.inviata && s.motiviNonInviabile.length > 0 && (
                        // Per esteso, non in un tooltip: è la lista di cosa andare a correggere, e
                        // un'informazione che si deve poter leggere senza cercarla.
                        <Box sx={{ mt: 0.75, fontSize: 12, color: tokens.orange700, maxWidth: 360 }}>
                          {s.motiviNonInviabile.join('; ')}
                        </Box>
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {!s.inviata && (
                        <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
                          {inviabile(s) && puoInviare && (
                            <Button
                              size="small"
                              variant="contained"
                              disabled={inviaSingola.isPending}
                              onClick={() => inviaUnaSchedina(s.ospiteId, s.nomeOspite)}
                            >
                              Invia
                            </Button>
                          )}
                          <Button size="small" variant="outlined" onClick={() => esportaSingola(s.ospiteId)}>
                            Scarica
                          </Button>
                        </Box>
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
