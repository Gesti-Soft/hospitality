import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import SendIcon from '@mui/icons-material/SendOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useTipologie } from '../api/tipologie'
import { ApiError } from '../api/client'
import { useInviaOsservatorioOra, useOsservatorioAppartamenti, type OsservatorioAppartamentoDto } from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { OsservatorioAppartamentoDialog } from '../components/OsservatorioAppartamentoDialog'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function OsservatorioPage() {
  const { strutturaId } = useStruttura()
  const appartamenti = useOsservatorioAppartamenti(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | OsservatorioAppartamentoDto>('chiuso')
  const [errore, setErrore] = useState<string | null>(null)
  const [risultati, setRisultati] = useState<Record<string, string>>({})

  const invia = useInviaOsservatorioOra(strutturaId)

  function inviaOra(a: OsservatorioAppartamentoDto) {
    setErrore(null)
    invia.mutate(a.id, {
      onSuccess: (r) =>
        setRisultati((prec) => ({
          ...prec,
          [a.id]: `${r.arriviInviati} arrivi, ${r.checkoutInviati} check-out, ${r.giorniChiusi} giorni chiusi.${r.messaggio ? ` ${r.messaggio}` : ''}`,
        })),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero di arrivi/partenze all'Osservatorio Turistico regionale. Una struttura può avere più appartamenti/entità PMS,
        ognuno con le proprie credenziali e un sottoinsieme di tipologie camera.
      </Typography>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Appartamenti</Typography>
        <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuovo')} disabled={!strutturaId}>
          + Nuovo appartamento
        </Button>
      </Box>

      {(appartamenti.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={220} />}

      {!appartamenti.isLoading && !tipologie.isLoading && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell>Credenziali</TableCell>
                <TableCell>Ultimo invio</TableCell>
                <TableCell>Ultimo errore</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(appartamenti.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun appartamento configurato.
                  </TableCell>
                </TableRow>
              )}
              {(appartamenti.data ?? []).map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{a.nome}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={a.credenzialiConfigurate ? 'Configurate' : 'Da configurare'}
                      sx={{ bgcolor: a.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontFamily: fontMono, fontSize: 12.5 }}>
                    {a.ultimoInvioAtUtc ? formattatoreDataOra.format(new Date(a.ultimoInvioAtUtc)) : 'mai eseguito'}
                    {risultati[a.id] && (
                      <Typography sx={{ fontSize: 11, color: tokens.textSecondary, mt: 0.25 }}>{risultati[a.id]}</Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12, color: tokens.error600 }}>{a.ultimoErrore ?? '—'}</TableCell>
                  <TableCell align="right">
                    <Tooltip title="Invia ora">
                      <IconButton size="small" onClick={() => inviaOra(a)} disabled={invia.isPending}>
                        <SendIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Modifica">
                      <IconButton size="small" onClick={() => setDialogo(a)}>
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
        <OsservatorioAppartamentoDialog
          strutturaId={strutturaId}
          appartamento={dialogo === 'nuovo' ? null : dialogo}
          tipologie={tipologie.data ?? []}
          onClose={() => setDialogo('chiuso')}
        />
      )}
    </Box>
  )
}
