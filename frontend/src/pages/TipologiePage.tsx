import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useEliminaTipologia, useTipologie, type TipologiaCameraDto } from '../api/tipologie'
import { ApiError } from '../api/client'
import { fontDisplay, fontMono, tokens } from '../theme'
import { TipologiaDialog } from '../components/TipologiaDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { usePuoScrivere } from '../permessi/usePuoScrivere'
import { useMobile } from '../lib/useMobile'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../components/CardElenco'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

export function TipologiePage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const [errore, setErrore] = useState<string | null>(null)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | TipologiaCameraDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<TipologiaCameraDto | null>(null)

  const tipologie = useTipologie(strutturaId)
  const elimina = useEliminaTipologia(strutturaId)

  function confermaElimina() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, {
      onSuccess: () => setDaEliminare(null),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Tipologie camera</Typography>
        {puoScrivere && <BottoneNuovo etichetta="+ Nuova tipologia" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} />}
      </Box>

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {tipologie.isLoading && <Skeleton variant="rounded" height={220} />}

      {!tipologie.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(tipologie.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessuna tipologia configurata." />}
          {(tipologie.data ?? []).map((t) => (
            <CardElenco key={t.id}>
              <TestataCardElenco titolo={t.tipologiaCamera} />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Prezzo default', valore: t.prezzoDefault != null ? formattatoreValuta.format(t.prezzoDefault) : '—' },
                  { etichetta: 'Ospiti inclusi', valore: t.numeroImplementoPersona },
                  { etichetta: 'Supplemento persona', valore: formattatoreValuta.format(t.implemento) },
                  { etichetta: 'Cauzione', valore: t.cauzione != null ? formattatoreValuta.format(t.cauzione) : '—' },
                ]}
              />
              {puoScrivere && (
                <AzioniCardElenco>
                  <IconButton size="small" onClick={() => setDialogo(t)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => setDaEliminare(t)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
        </Box>
      )}

      {!tipologie.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell align="right">Prezzo default</TableCell>
                <TableCell align="right">Ospiti inclusi</TableCell>
                <TableCell align="right">Supplemento persona</TableCell>
                <TableCell align="right">Cauzione</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(tipologie.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna tipologia configurata.
                  </TableCell>
                </TableRow>
              )}
              {(tipologie.data ?? []).map((t) => (
                <TableRow key={t.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{t.tipologiaCamera}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.prezzoDefault != null ? formattatoreValuta.format(t.prezzoDefault) : '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.numeroImplementoPersona}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {formattatoreValuta.format(t.implemento)}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {t.cauzione != null ? formattatoreValuta.format(t.cauzione) : '—'}
                  </TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <>
                        <IconButton size="small" onClick={() => setDialogo(t)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton size="small" onClick={() => setDaEliminare(t)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <TipologiaDialog strutturaId={strutturaId} tipologia={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare tipologia"
          messaggio={`Eliminare la tipologia "${daEliminare.tipologiaCamera}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaElimina}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
