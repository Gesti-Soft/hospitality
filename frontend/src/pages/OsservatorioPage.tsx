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
import { useAnniOsservatorio, useInviaOsservatorioOra, useOsservatorioAppartamenti, useSchedineOsservatorio } from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { usePuoScrivere } from '../permessi/usePuoScrivere'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../lib/anni'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

export function OsservatorioPage() {
  const { strutturaId } = useStruttura()
  const puoInviare = usePuoScrivere('statePoliceWrite')
  const appartamenti = useOsservatorioAppartamenti(strutturaId)
  const [appartamentoId, setAppartamentoId] = useState<string | null>(null)
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniOsservatorio(strutturaId)
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)
  const [errore, setErrore] = useState<string | null>(null)
  const [risultato, setRisultato] = useState<string | null>(null)

  useEffect(() => {
    const lista = appartamenti.data ?? []
    if (lista.length > 0 && (appartamentoId === null || !lista.some((a) => a.id === appartamentoId))) {
      setAppartamentoId(lista[0].id)
    }
    if (lista.length === 0 && appartamentoId !== null) {
      setAppartamentoId(null)
    }
  }, [appartamenti.data, appartamentoId])

  const schedine = useSchedineOsservatorio(strutturaId, appartamentoId, anno)
  const invia = useInviaOsservatorioOra(strutturaId)

  function inviaOra() {
    if (!appartamentoId) return
    setErrore(null)
    setRisultato(null)
    invia.mutate(appartamentoId, {
      onSuccess: (r) => setRisultato(`${r.arriviInviati} arrivi, ${r.checkoutInviati} check-out, ${r.giorniChiusi} giorni chiusi.${r.messaggio ? ` ${r.messaggio}` : ''}`),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const appartamentoSelezionato = (appartamenti.data ?? []).find((a) => a.id === appartamentoId) ?? null

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
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <TextField
            select
            size="small"
            label="Appartamento"
            value={appartamentoId ?? ''}
            onChange={(e) => setAppartamentoId(e.target.value)}
            sx={{ minWidth: 240 }}
          >
            {(appartamenti.data ?? []).map((a) => (
              <MenuItem key={a.id} value={a.id}>
                {a.nome}
              </MenuItem>
            ))}
          </TextField>

          {appartamentoSelezionato && (
            <Chip
              size="small"
              label={appartamentoSelezionato.credenzialiConfigurate ? 'Credenziali configurate' : 'Credenziali non configurate'}
              sx={{ bgcolor: appartamentoSelezionato.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }}
            />
          )}

          {puoInviare && (
            <Button variant="contained" color="primary" size="small" onClick={inviaOra} disabled={invia.isPending || !appartamentoId}>
              Invia ora
            </Button>
          )}
        </Box>
      )}

      {appartamentoSelezionato?.ultimoErrore && <Alert severity="warning">{appartamentoSelezionato.ultimoErrore}</Alert>}
      {risultato && (
        <Alert severity="info" onClose={() => setRisultato(null)}>
          {risultato}
        </Alert>
      )}

      {appartamentoId && (
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
          {!schedine.isLoading && (
            <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Ospite</TableCell>
                    <TableCell>Camera</TableCell>
                    <TableCell>Check-in</TableCell>
                    <TableCell>Check-out</TableCell>
                    <TableCell>Arrivo</TableCell>
                    <TableCell>Partenza</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(schedine.data ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                        Nessun arrivo/partenza per l'anno selezionato.
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
                        <Chip size="small" label={s.arrivoInviato ? 'Inviato' : 'Da inviare'} sx={{ bgcolor: s.arrivoInviato ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
                      </TableCell>
                      <TableCell>
                        {s.partenzaInviata === null ? (
                          '—'
                        ) : (
                          <Chip size="small" label={s.partenzaInviata ? 'Inviata' : 'Da inviare'} sx={{ bgcolor: s.partenzaInviata ? tokens.ok600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
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
