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
  useAnniOsservatorio,
  useInviaArrivoOsservatorioSingolo,
  useInviaOsservatorioOra,
  useOsservatorioAppartamenti,
  useSchedineOsservatorio,
  useStatoOsservatorio,
} from '../../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

export function OsservatorioPage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoInviare = usePuoScrivere('statePoliceWrite')
  const appartamenti = useOsservatorioAppartamenti(strutturaId)
  // La giornata da chiudere viene letta dal servizio Osservatorio, non dalla nostra cache: è l'unico
  // dato che dice davvero cosa è ancora trasmissibile.
  const stato = useStatoOsservatorio(strutturaId)
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniOsservatorio(strutturaId)
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)
  const [errore, setErrore] = useState<string | null>(null)
  const [risultato, setRisultato] = useState<string | null>(null)

  const schedine = useSchedineOsservatorio(strutturaId, anno)
  const invia = useInviaOsservatorioOra(strutturaId)
  const inviaSingolo = useInviaArrivoOsservatorioSingolo(strutturaId)

  function inviaUnArrivo(ospiteId: string, nomeOspite: string) {
    setErrore(null)
    setRisultato(null)
    // L'appartamento non si passa: lo deduce il server dalla tipologia della camera dell'ospite,
    // così la schedina finisce sempre dov'è giusto anche se nel selettore qui sopra ce n'è un altro.
    inviaSingolo.mutate(
      ospiteId,
      {
        // Stesso schema dell'invio dell'intero appartamento, qui sopra: esito e messaggi restano
        // negli Alert in cima alla pagina, dove l'operatore può leggerli con calma (un invio può
        // riuscire a metà o essere bloccato dalle giornate non ancora chiuse).
        onSuccess: (r) =>
          r.arriviInviati > 0
            ? setRisultato(`Arrivo di ${nomeOspite} inviato.`)
            : setErrore(r.messaggio ?? "Invio non riuscito: l'arrivo resta da trasmettere."),
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  function inviaOra() {
    setErrore(null)
    setRisultato(null)
    invia.mutate(undefined, {
      onSuccess: (r) => setRisultato(`${r.arriviInviati} arrivi, ${r.checkoutInviati} check-out, ${r.giorniChiusi} giorni chiusi.${r.messaggio ? ` ${r.messaggio}` : ''}`),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }


  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero di arrivi/partenze all'Osservatorio Turistico regionale. Appartamenti e credenziali si configurano in
        Impostazioni.
      </Typography>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      {appartamenti.isLoading && <Skeleton variant="rounded" height={60} />}

      {!appartamenti.isLoading && (appartamenti.data ?? []).length === 0 && (
        <Alert severity="info">Nessun appartamento configurato. Aggiungine uno in Impostazioni &gt; Osservatorio Turistico.</Alert>
      )}

      {!appartamenti.isLoading && (appartamenti.data ?? []).length > 0 && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
          {/* Niente più scelta dell'appartamento: ogni schedina sa già dove va dichiarata, e "Invia
              ora" li processa tutti. Restano visibili solo gli appartamenti configurati male, che
              sono l'unica cosa su cui l'operatore debba intervenire. */}
          {/* Il giorno di chiusura è l'informazione che decide tutto qui dentro: gli arrivi
              trasmissibili sono solo quelli della giornata ancora da chiudere, quindi va letto a
              colpo d'occhio per ogni appartamento. */}
          {stato.isLoading && <Skeleton variant="rounded" width={260} height={24} />}
          {!stato.isLoading &&
            (stato.data ?? []).map((a) => (
              <Chip
                key={a.appartamentoId}
                size="small"
                label={
                  a.chiusoFinoA
                    ? `${a.nome}: fermo al ${formattatoreData.format(new Date(a.chiusoFinoA))}`
                    : `${a.nome}: ${a.errore ?? 'stato non disponibile'}`
                }
                sx={{
                  bgcolor: a.chiusoFinoA ? tokens.blue100 : tokens.textTertiary,
                  color: a.chiusoFinoA ? tokens.blue700 : '#fff',
                  fontWeight: 700,
                }}
              />
            ))}

          {puoInviare && (
            <Button variant="contained" color="primary" size="small" onClick={inviaOra} disabled={invia.isPending}>
              Invia ora
            </Button>
          )}
        </Box>
      )}

      {(stato.data ?? [])
        .filter((a) => a.errore && a.chiusoFinoA)
        .map((a) => (
          <Alert key={a.appartamentoId} severity="warning">
            {a.nome}: {a.errore} — mostrata l'ultima data nota.
          </Alert>
        ))}

      {(appartamenti.data ?? [])
        .filter((a) => a.ultimoErrore)
        .map((a) => (
          <Alert key={a.id} severity="warning">
            {a.nome}: {a.ultimoErrore}
          </Alert>
        ))}
      {risultato && (
        <Alert severity="info" onClose={() => setRisultato(null)}>
          {risultato}
        </Alert>
      )}

      {/* Elenco di tutta la Struttura: non dipende più dall'appartamento scelto qui sopra (che serve
          solo all'invio giornaliero e alle credenziali) — ogni riga dice da sé dove va dichiarata. */}
      {(
        <Box>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Arrivi/partenze</Typography>
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
              {(schedine.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessun arrivo/partenza per l'anno selezionato." />}
              {(schedine.data ?? []).map((s) => (
                <CardElenco key={s.ospiteId}>
                  <TestataCardElenco
                    titolo={s.nomeOspite}
                    azioneDestra={
                      <Box sx={{ display: 'flex', gap: 0.5 }}>
                        <Chip
                          size="small"
                          label={s.arrivoInviato ? 'Arrivo inviato' : s.inTermine ? 'Arrivo da inviare' : 'Giornata chiusa'}
                          sx={{ bgcolor: s.arrivoInviato ? tokens.ok600 : s.inTermine ? tokens.wait600 : tokens.error600, color: '#fff', fontWeight: 700 }}
                        />
                        {s.partenzaInviata !== null && (
                          <Chip size="small" label={s.partenzaInviata ? 'Partenza inviata' : 'Partenza da inviare'} sx={{ bgcolor: s.partenzaInviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                        )}
                      </Box>
                    }
                  />
                  <RigaCardMeta
                    voci={[
                      { etichetta: 'Camera', valore: s.camera ?? '—' },
                      { etichetta: 'Appartamento', valore: s.appartamentoNome ?? 'Nessuno: tipologia non associata' },
                      { etichetta: 'Check-in', valore: s.checkIn ? formattatoreData.format(new Date(s.checkIn)) : '—' },
                      { etichetta: 'Check-out', valore: s.checkOut ? formattatoreData.format(new Date(s.checkOut)) : '—' },
                    ]}
                  />
                  {!s.arrivoInviato && puoInviare && s.inTermine && (
                    <AzioniCardElenco>
                      <Button size="small" variant="contained" disabled={inviaSingolo.isPending} onClick={() => inviaUnArrivo(s.ospiteId, s.nomeOspite)}>
                        Invia
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
                    <TableCell>Appartamento</TableCell>
                    <TableCell>Check-in</TableCell>
                    <TableCell>Check-out</TableCell>
                    <TableCell>Arrivo</TableCell>
                    <TableCell>Partenza</TableCell>
                    <TableCell align="right">Azioni</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(schedine.data ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                        Nessun arrivo/partenza per l'anno selezionato.
                      </TableCell>
                    </TableRow>
                  )}
                  {(schedine.data ?? []).map((s) => (
                    <TableRow key={s.ospiteId} hover>
                      <TableCell sx={{ fontWeight: 700 }}>{s.nomeOspite}</TableCell>
                      <TableCell>{s.camera ?? '—'}</TableCell>
                      <TableCell sx={{ fontSize: 12.5, color: s.appartamentoNome ? tokens.textSecondary : tokens.error600 }}>
                        {s.appartamentoNome ?? 'Nessuno: tipologia non associata'}
                      </TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{s.checkIn ? formattatoreData.format(new Date(s.checkIn)) : '—'}</TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{s.checkOut ? formattatoreData.format(new Date(s.checkOut)) : '—'}</TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={s.arrivoInviato ? 'Inviato' : s.inTermine ? 'Da inviare' : 'Giornata chiusa'}
                          sx={{ bgcolor: s.arrivoInviato ? tokens.ok600 : s.inTermine ? tokens.wait600 : tokens.error600, color: '#fff', fontWeight: 700 }}
                        />
                      </TableCell>
                      <TableCell>
                        {s.partenzaInviata === null ? (
                          '—'
                        ) : (
                          <Chip size="small" label={s.partenzaInviata ? 'Inviata' : 'Da inviare'} sx={{ bgcolor: s.partenzaInviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                        )}
                      </TableCell>
                      <TableCell align="right">
                        {!s.arrivoInviato && s.inTermine && puoInviare && (
                          <Button size="small" variant="contained" disabled={inviaSingolo.isPending} onClick={() => inviaUnArrivo(s.ospiteId, s.nomeOspite)}>
                            Invia
                          </Button>
                        )}
                        {!s.arrivoInviato && !s.inTermine && (
                          <Typography sx={{ fontSize: 11.5, color: tokens.error600, fontWeight: 700 }}>
                            Giornata chiusa
                          </Typography>
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
