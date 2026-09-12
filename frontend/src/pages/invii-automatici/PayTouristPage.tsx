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
  esportaPayTourist,
  useAnniPayTourist,
  useInviaPayTouristOra,
  useInviaPayTouristSingola,
  usePayTouristStrutture,
  usePrenotazioniPayTourist,
} from '../../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function PayTouristPage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoInviare = usePuoScrivere('statePoliceWrite')
  const strutture = usePayTouristStrutture(strutturaId)
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniPayTourist(strutturaId)
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)
  const [errore, setErrore] = useState<string | null>(null)
  const [risultatoInvio, setRisultatoInvio] = useState<string | null>(null)

  const prenotazioni = usePrenotazioniPayTourist(strutturaId, anno)
  const invia = useInviaPayTouristOra(strutturaId)
  const inviaSingola = useInviaPayTouristSingola(strutturaId)
  const [invioSingoloInCorso, setInvioSingoloInCorso] = useState<string | null>(null)

  function eseguiInviaSingola(ospiteId: string) {
    setErrore(null)
    setInvioSingoloInCorso(ospiteId)
    // La struttura PayTourist non si passa: la deduce il server dalla tipologia della camera
    // dell'ospite, così la prenotazione finisce sempre dove va dichiarata.
    inviaSingola.mutate(
      ospiteId,
      {
        onSuccess: () => setInvioSingoloInCorso(null),
        onError: (err) => {
          setInvioSingoloInCorso(null)
          setErrore(err instanceof ApiError ? err.message : 'Invio non riuscito, riprova.')
        },
      },
    )
  }

  function inviaOra() {
    setErrore(null)
    setRisultatoInvio(null)
    invia.mutate(undefined, {
      onSuccess: (r) => setRisultatoInvio(`${r.inviate}/${r.totalePrenotazioni} prenotazioni inviate.${r.messaggio ? ` ${r.messaggio}` : ''}`),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  async function esporta() {
    if (!strutturaId) return
    try {
      await esportaPayTourist(strutturaId)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }


  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero delle prenotazioni completate a PayTourist per la tassa di soggiorno. Token e strutture PayTourist si
        configurano in Impostazioni.
      </Typography>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      {strutture.isLoading && <Skeleton variant="rounded" height={60} />}

      {!strutture.isLoading && (strutture.data ?? []).length === 0 && (
        <Alert severity="info">Nessuna struttura PayTourist configurata. Aggiungine una in Impostazioni &gt; PayTourist.</Alert>
      )}

      {!strutture.isLoading && (strutture.data ?? []).length > 0 && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
          {/* Niente più scelta della struttura PayTourist: ogni prenotazione sa già dove va
              dichiarata, e "Invia ora tutte" le processa tutte. Il menu qui resta solo per scegliere
              cosa esportare, e compare unicamente quando le strutture sono più di una. */}
          {puoInviare && (
            <Button variant="contained" color="primary" size="small" onClick={inviaOra} disabled={invia.isPending}>
              Invia ora tutte
            </Button>
          )}

          <Button variant="outlined" size="small" onClick={esporta} disabled={(prenotazioni.data ?? []).length === 0}>
            Esporta JSON
          </Button>
        </Box>
      )}

      {/* Stato dell'ultimo invio per ciascuna struttura configurata, non più solo di quella scelta. */}
      {(strutture.data ?? []).length > 0 && (
        <Box sx={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {(strutture.data ?? []).map((s) => (
            <Campo
              key={s.id}
              etichetta={s.nome ?? 'Struttura PayTourist'}
              valore={s.ultimoInvioAtUtc ? `${formattatoreDataOra.format(new Date(s.ultimoInvioAtUtc))} — ${s.ultimeInviate ?? 0} inviate` : 'mai eseguito'}
            />
          ))}
        </Box>
      )}

      {(strutture.data ?? [])
        .filter((s) => s.ultimoErrore)
        .map((s) => (
          <Alert key={s.id} severity="warning">
            {s.nome}: {s.ultimoErrore}
          </Alert>
        ))}
      {risultatoInvio && (
        <Alert severity="info" onClose={() => setRisultatoInvio(null)}>
          {risultatoInvio}
        </Alert>
      )}

      {/* Elenco di tutta la Struttura: non dipende più dalla struttura PayTourist scelta qui sopra. */}
      {(
        <Box>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Prenotazioni</Typography>
            <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
              {anniSelezionabili.map((a) => (
                <MenuItem key={a} value={a}>
                  {a}
                </MenuItem>
              ))}
            </TextField>
          </Box>
          {prenotazioni.isLoading && <Skeleton variant="rounded" height={220} />}

          {!prenotazioni.isLoading && mobile && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {(prenotazioni.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessuna prenotazione per l'anno selezionato." />}
              {(prenotazioni.data ?? []).map((p) => (
                <CardElenco key={p.ospiteId}>
                  <TestataCardElenco
                    titolo={p.nomeOspite}
                    azioneDestra={
                      <Chip
                        size="small"
                        label={p.inviata ? 'Inviata' : p.inTermine ? 'Da inviare' : 'Fuori termine'}
                        sx={{ bgcolor: p.inviata ? tokens.ok600 : p.inTermine ? tokens.wait600 : tokens.error600, color: '#fff', fontWeight: 700 }}
                      />
                    }
                  />
                  <RigaCardMeta
                    voci={[
                      { etichetta: 'Camera', valore: p.camera ?? '—' },
                      { etichetta: 'Struttura PayTourist', valore: p.payTouristStrutturaNome ?? 'Nessuna: tipologia non associata' },
                      { etichetta: 'Check-in', valore: p.checkIn ? formattatoreData.format(new Date(p.checkIn)) : '—' },
                      { etichetta: 'Check-out', valore: p.checkOut ? formattatoreData.format(new Date(p.checkOut)) : '—' },
                    ]}
                  />
                  {!p.inviata && p.inTermine && puoInviare && (
                    <AzioniCardElenco>
                      <Button
                        size="small"
                        variant="outlined"
                        onClick={() => eseguiInviaSingola(p.ospiteId)}
                        disabled={invioSingoloInCorso === p.ospiteId}
                      >
                        {invioSingoloInCorso === p.ospiteId ? 'Invio…' : 'Invia'}
                      </Button>
                    </AzioniCardElenco>
                  )}
                </CardElenco>
              ))}
            </Box>
          )}

          {!prenotazioni.isLoading && !mobile && (
            <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Ospite</TableCell>
                    <TableCell>Camera</TableCell>
                    <TableCell>Struttura PayTourist</TableCell>
                    <TableCell>Check-in</TableCell>
                    <TableCell>Check-out</TableCell>
                    <TableCell>Termine invio</TableCell>
                    <TableCell>Stato</TableCell>
                    <TableCell align="right">Azioni</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(prenotazioni.data ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                        Nessuna prenotazione per l'anno selezionato.
                      </TableCell>
                    </TableRow>
                  )}
                  {(prenotazioni.data ?? []).map((p) => (
                    <TableRow key={p.ospiteId} hover>
                      <TableCell sx={{ fontWeight: 700 }}>{p.nomeOspite}</TableCell>
                      <TableCell>{p.camera ?? '—'}</TableCell>
                      <TableCell sx={{ fontSize: 12.5, color: p.payTouristStrutturaNome ? tokens.textSecondary : tokens.error600 }}>
                        {p.payTouristStrutturaNome ?? 'Nessuna: tipologia non associata'}
                      </TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{p.checkIn ? formattatoreData.format(new Date(p.checkIn)) : '—'}</TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{p.checkOut ? formattatoreData.format(new Date(p.checkOut)) : '—'}</TableCell>
                      <TableCell sx={{ fontFamily: fontMono, fontSize: 12.5, color: p.inviata || p.inTermine ? tokens.textSecondary : tokens.error600 }}>
                        {p.inviata || !p.scadenzaInvioUtc
                          ? '—'
                          : `${p.inTermine ? 'entro' : 'scaduto il'} ${formattatoreData.format(new Date(p.scadenzaInvioUtc))}`}
                      </TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={p.inviata ? 'Inviata' : p.inTermine ? 'Da inviare' : 'Fuori termine'}
                          sx={{ bgcolor: p.inviata ? tokens.ok600 : p.inTermine ? tokens.wait600 : tokens.error600, color: '#fff', fontWeight: 700 }}
                        />
                      </TableCell>
                      <TableCell align="right">
                        {!p.inviata && p.inTermine && puoInviare && (
                          <Button
                            size="small"
                            variant="outlined"
                            onClick={() => eseguiInviaSingola(p.ospiteId)}
                            disabled={invioSingoloInCorso === p.ospiteId}
                          >
                            {invioSingoloInCorso === p.ospiteId ? 'Invio…' : 'Invia'}
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
      )}
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
