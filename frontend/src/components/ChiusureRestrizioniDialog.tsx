import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { ApiError } from '../api/client'
import {
  useChiusureCamera,
  useCreaChiusuraCamera,
  useCreaRestrizionePeriodoCamera,
  useEliminaChiusuraCamera,
  useEliminaRestrizionePeriodoCamera,
  useRestrizioniPeriodoCamera,
} from '../api/integrazioni'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { CampoData } from './CampoData'
import { fontDisplay, fontMono, tokens } from '../theme'

interface Props {
  strutturaId: string
  cameraId: string
  cameraNome: string
  onClose: () => void
}

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

export function ChiusureRestrizioniDialog({ strutturaId, cameraId, cameraNome, onClose }: Props) {
  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Chiusure e restrizioni — {cameraNome}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 3, pt: 1 }}>
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
          Da qui puoi bloccare la camera per un periodo (manutenzione) o impostare un soggiorno minimo/massimo valido solo in certe date.
          Le modifiche si applicano su Wubook al prossimo "Sincronizza disponibilità".
        </Typography>
        <SezioneChiusure strutturaId={strutturaId} cameraId={cameraId} />
        <SezioneRestrizioni strutturaId={strutturaId} cameraId={cameraId} />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose}>Chiudi</Button>
      </DialogActions>
    </Dialog>
  )
}

function SezioneChiusure({ strutturaId, cameraId }: { strutturaId: string; cameraId: string }) {
  const chiusure = useChiusureCamera(strutturaId, cameraId)
  const crea = useCreaChiusuraCamera(strutturaId, cameraId)
  const elimina = useEliminaChiusuraCamera(strutturaId, cameraId)

  const [dataInizio, setDataInizio] = useState(formatoInputData(new Date()))
  const [dataFine, setDataFine] = useState(formatoInputData(new Date()))
  const [quantita, setQuantita] = useState('1')
  const [motivo, setMotivo] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [filtroDa, setFiltroDa] = useState('')
  const [filtroA, setFiltroA] = useState('')

  function aggiungi() {
    const inizio = parsaInputData(dataInizio)
    const fine = parsaInputData(dataFine)
    if (fine < inizio) {
      setErrore('La data di fine deve essere successiva o uguale alla data di inizio.')
      return
    }
    setErrore(null)
    crea.mutate(
      {
        dataInizio: isoLocale(inizio),
        dataFine: isoLocale(fine),
        motivo: motivo.trim() === '' ? null : motivo.trim(),
        quantita: quantita.trim() === '' ? null : Number(quantita),
      },
      { onSuccess: () => setMotivo(''), onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') },
    )
  }

  const righe = (chiusure.data ?? []).filter((c) => {
    if (filtroDa && c.dataFine.slice(0, 10) < filtroDa) return false
    if (filtroA && c.dataInizio.slice(0, 10) > filtroA) return false
    return true
  })

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 14 }}>Chiusure (manutenzione + prenotazioni correnti)</Typography>
      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center' }}>
        <CampoData label="Filtro da" value={filtroDa} onChange={setFiltroDa} size="small" />
        <CampoData label="Filtro a" value={filtroA} onChange={setFiltroA} size="small" />
        {(filtroDa || filtroA) && (
          <Button size="small" onClick={() => { setFiltroDa(''); setFiltroA('') }}>Pulisci</Button>
        )}
      </Box>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Dal</TableCell>
              <TableCell>Al</TableCell>
              <TableCell>Quantità</TableCell>
              <TableCell>Motivo</TableCell>
              <TableCell align="right">Azioni</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {righe.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 2 }}>
                  Nessuna chiusura.
                </TableCell>
              </TableRow>
            )}
            {righe.map((c) => (
              <TableRow key={c.id ?? `${c.dataInizio}-${c.dataFine}-${c.motivo}`} hover>
                <TableCell sx={{ fontFamily: fontMono }}>{formattatoreData.format(new Date(c.dataInizio))}</TableCell>
                <TableCell sx={{ fontFamily: fontMono }}>{formattatoreData.format(new Date(c.dataFine))}</TableCell>
                <TableCell>{c.quantita ?? '—'}</TableCell>
                <TableCell>{c.motivo ?? '—'}</TableCell>
                <TableCell align="right">
                  {c.id != null && (
                    <IconButton size="small" onClick={() => elimina.mutate(c.id!)} disabled={elimina.isPending}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>

      <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center', flexWrap: 'wrap' }}>
        <CampoData label="Dal" value={dataInizio} onChange={setDataInizio} size="small" disabled={crea.isPending} />
        <CampoData label="Al" value={dataFine} onChange={setDataFine} min={dataInizio || undefined} size="small" disabled={crea.isPending} />
        <TextField label="Quantità" type="number" value={quantita} onChange={(e) => setQuantita(e.target.value)} size="small" sx={{ width: 110 }} disabled={crea.isPending} />
        <TextField label="Motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} size="small" disabled={crea.isPending} />
        <Button variant="outlined" size="small" onClick={aggiungi} disabled={crea.isPending} sx={{ whiteSpace: 'nowrap' }}>
          + Chiudi periodo
        </Button>
      </Box>
    </Box>
  )
}

function SezioneRestrizioni({ strutturaId, cameraId }: { strutturaId: string; cameraId: string }) {
  const restrizioni = useRestrizioniPeriodoCamera(strutturaId, cameraId)
  const crea = useCreaRestrizionePeriodoCamera(strutturaId, cameraId)
  const elimina = useEliminaRestrizionePeriodoCamera(strutturaId, cameraId)

  const [dataInizio, setDataInizio] = useState(formatoInputData(new Date()))
  const [dataFine, setDataFine] = useState(formatoInputData(new Date()))
  const [minStay, setMinStay] = useState('')
  const [maxStay, setMaxStay] = useState('')
  const [motivo, setMotivo] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [filtroDa, setFiltroDa] = useState('')
  const [filtroA, setFiltroA] = useState('')

  function aggiungi() {
    const inizio = parsaInputData(dataInizio)
    const fine = parsaInputData(dataFine)
    if (fine < inizio) {
      setErrore('La data di fine deve essere successiva o uguale alla data di inizio.')
      return
    }
    if (minStay.trim() === '' && maxStay.trim() === '') {
      setErrore('Indica almeno un soggiorno minimo o massimo.')
      return
    }
    setErrore(null)
    crea.mutate(
      {
        dataInizio: isoLocale(inizio),
        dataFine: isoLocale(fine),
        minStay: minStay.trim() === '' ? null : Number(minStay),
        maxStay: maxStay.trim() === '' ? null : Number(maxStay),
        motivo: motivo.trim() === '' ? null : motivo.trim(),
      },
      { onSuccess: () => { setMinStay(''); setMaxStay(''); setMotivo('') }, onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') },
    )
  }

  const righe = (restrizioni.data ?? []).filter((r) => {
    if (filtroDa && r.dataFine.slice(0, 10) < filtroDa) return false
    if (filtroA && r.dataInizio.slice(0, 10) > filtroA) return false
    return true
  })

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 14 }}>Soggiorno minimo/massimo per periodo</Typography>
      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}

      <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center' }}>
        <CampoData label="Filtro da" value={filtroDa} onChange={setFiltroDa} size="small" />
        <CampoData label="Filtro a" value={filtroA} onChange={setFiltroA} size="small" />
        {(filtroDa || filtroA) && (
          <Button size="small" onClick={() => { setFiltroDa(''); setFiltroA('') }}>Pulisci</Button>
        )}
      </Box>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Dal</TableCell>
              <TableCell>Al</TableCell>
              <TableCell>Min</TableCell>
              <TableCell>Max</TableCell>
              <TableCell>Motivo</TableCell>
              <TableCell align="right">Azioni</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {righe.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 2 }}>
                  Nessuna regola per periodo.
                </TableCell>
              </TableRow>
            )}
            {righe.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell sx={{ fontFamily: fontMono }}>{formattatoreData.format(new Date(r.dataInizio))}</TableCell>
                <TableCell sx={{ fontFamily: fontMono }}>{formattatoreData.format(new Date(r.dataFine))}</TableCell>
                <TableCell>{r.minStay ?? '—'}</TableCell>
                <TableCell>{r.maxStay ?? '—'}</TableCell>
                <TableCell>{r.motivo ?? '—'}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => elimina.mutate(r.id)} disabled={elimina.isPending}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>

      <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'center', flexWrap: 'wrap' }}>
        <CampoData label="Dal" value={dataInizio} onChange={setDataInizio} size="small" disabled={crea.isPending} />
        <CampoData label="Al" value={dataFine} onChange={setDataFine} min={dataInizio || undefined} size="small" disabled={crea.isPending} />
        <TextField label="Min notti" type="number" value={minStay} onChange={(e) => setMinStay(e.target.value)} size="small" sx={{ width: 110 }} disabled={crea.isPending} />
        <TextField label="Max notti" type="number" value={maxStay} onChange={(e) => setMaxStay(e.target.value)} size="small" sx={{ width: 110 }} disabled={crea.isPending} />
        <TextField label="Motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} size="small" disabled={crea.isPending} />
        <Button variant="outlined" size="small" onClick={aggiungi} disabled={crea.isPending} sx={{ whiteSpace: 'nowrap' }}>
          + Aggiungi regola
        </Button>
      </Box>
    </Box>
  )
}
