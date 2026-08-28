import { useEffect, useState } from 'react'
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
import { esportaPayTourist, useInviaPayTouristOra, useInviaPayTouristSingola, usePayTouristStrutture, usePrenotazioniPayTourist } from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function PayTouristPage() {
  const { strutturaId } = useStruttura()
  const strutture = usePayTouristStrutture(strutturaId)
  const [payTouristStrutturaId, setPayTouristStrutturaId] = useState<string | null>(null)
  const [errore, setErrore] = useState<string | null>(null)
  const [risultatoInvio, setRisultatoInvio] = useState<string | null>(null)

  useEffect(() => {
    const lista = strutture.data ?? []
    if (lista.length > 0 && (payTouristStrutturaId === null || !lista.some((s) => s.id === payTouristStrutturaId))) {
      setPayTouristStrutturaId(lista[0].id)
    }
    if (lista.length === 0 && payTouristStrutturaId !== null) {
      setPayTouristStrutturaId(null)
    }
  }, [strutture.data, payTouristStrutturaId])

  const prenotazioni = usePrenotazioniPayTourist(strutturaId, payTouristStrutturaId)
  const invia = useInviaPayTouristOra(strutturaId)
  const inviaSingola = useInviaPayTouristSingola(strutturaId)
  const [invioSingoloInCorso, setInvioSingoloInCorso] = useState<string | null>(null)

  function eseguiInviaSingola(ospiteId: string) {
    if (!payTouristStrutturaId) return
    setErrore(null)
    setInvioSingoloInCorso(ospiteId)
    inviaSingola.mutate(
      { payTouristStrutturaId, ospiteId },
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
    if (!strutturaId || !payTouristStrutturaId) return
    try {
      await esportaPayTourist(strutturaId, payTouristStrutturaId)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }

  const strutturaSelezionata = (strutture.data ?? []).find((s) => s.id === payTouristStrutturaId) ?? null

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
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <TextField
            select
            size="small"
            label="Struttura PayTourist"
            value={payTouristStrutturaId ?? ''}
            onChange={(e) => setPayTouristStrutturaId(e.target.value)}
            sx={{ minWidth: 240 }}
          >
            {(strutture.data ?? []).map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.nome}
              </MenuItem>
            ))}
          </TextField>

          <Button variant="contained" color="secondary" size="small" onClick={inviaOra} disabled={invia.isPending}>
            Invia ora tutte
          </Button>
          <Button variant="outlined" size="small" onClick={esporta} disabled={!payTouristStrutturaId || (prenotazioni.data ?? []).length === 0}>
            Esporta JSON
          </Button>
        </Box>
      )}

      {strutturaSelezionata && (
        <Box sx={{ display: 'flex', gap: 4 }}>
          <Campo etichetta="Ultimo invio" valore={strutturaSelezionata.ultimoInvioAtUtc ? formattatoreDataOra.format(new Date(strutturaSelezionata.ultimoInvioAtUtc)) : 'mai eseguito'} />
          <Campo etichetta="Inviate nell'ultimo invio" valore={String(strutturaSelezionata.ultimeInviate ?? '—')} />
        </Box>
      )}

      {strutturaSelezionata?.ultimoErrore && <Alert severity="warning">{strutturaSelezionata.ultimoErrore}</Alert>}
      {risultatoInvio && (
        <Alert severity="info" onClose={() => setRisultatoInvio(null)}>
          {risultatoInvio}
        </Alert>
      )}

      {payTouristStrutturaId && (
        <Box>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15, mb: 1.5 }}>Prenotazioni (ultimi 30 giorni)</Typography>
          {prenotazioni.isLoading && <Skeleton variant="rounded" height={220} />}
          {!prenotazioni.isLoading && (
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
                  {(prenotazioni.data ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                        Nessuna prenotazione negli ultimi 30 giorni.
                      </TableCell>
                    </TableRow>
                  )}
                  {(prenotazioni.data ?? []).map((p) => (
                    <TableRow key={p.ospiteId} hover>
                      <TableCell sx={{ fontWeight: 700 }}>{p.nomeOspite}</TableCell>
                      <TableCell>{p.camera ?? '—'}</TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{p.checkIn ? formattatoreData.format(new Date(p.checkIn)) : '—'}</TableCell>
                      <TableCell sx={{ fontFamily: fontMono }}>{p.checkOut ? formattatoreData.format(new Date(p.checkOut)) : '—'}</TableCell>
                      <TableCell>
                        <Chip size="small" label={p.inviata ? 'Inviata' : 'Da inviare'} sx={{ bgcolor: p.inviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                      </TableCell>
                      <TableCell align="right">
                        {!p.inviata && (
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
