import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import type { CameraDto } from '../api/camere'
import type { TipologiaCameraDto } from '../api/tipologie'
import { useImpostaPrezzo, type ImpostaPrezzoRequest } from '../api/prezzi'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'

interface Props {
  strutturaId: string
  camere: CameraDto[]
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function PrezzoDialog({ strutturaId, camere, tipologie, onClose }: Props) {
  const [ambito, setAmbito] = useState<'tipologia' | 'camera'>(tipologie.length > 0 ? 'tipologia' : 'camera')
  const [targetId, setTargetId] = useState('')
  const [dataInizio, setDataInizio] = useState(formatoInputData(new Date()))
  const [dataFine, setDataFine] = useState(formatoInputData(new Date()))
  const [prezzoPerNotte, setPrezzoPerNotte] = useState('')
  const [errore, setErrore] = useState<string | null>(null)

  const imposta = useImpostaPrezzo(strutturaId)

  function salva() {
    const inizio = parsaInputData(dataInizio)
    const fine = parsaInputData(dataFine)

    if (targetId === '') {
      setErrore(`Seleziona ${ambito === 'camera' ? 'una camera' : 'una tipologia'}.`)
      return
    }
    if (fine < inizio) {
      setErrore('La data di fine deve essere successiva o uguale alla data di inizio.')
      return
    }
    if (prezzoPerNotte.trim() === '' || Number(prezzoPerNotte) < 0) {
      setErrore('Indica un prezzo per notte valido.')
      return
    }
    setErrore(null)

    const request: ImpostaPrezzoRequest = {
      cameraId: ambito === 'camera' ? targetId : null,
      tipologiaId: ambito === 'tipologia' ? targetId : null,
      dataInizio: isoLocale(inizio),
      dataFine: isoLocale(fine),
      prezzoPerNotte: Number(prezzoPerNotte),
    }

    imposta.mutate(request, {
      onSuccess: onClose,
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuovo periodo di prezzo</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <ToggleButtonGroup
          exclusive
          value={ambito}
          onChange={(_, valore) => {
            if (valore) {
              setAmbito(valore)
              setTargetId('')
            }
          }}
          size="small"
        >
          <ToggleButton value="tipologia" disabled={imposta.isPending}>
            Per tipologia
          </ToggleButton>
          <ToggleButton value="camera" disabled={imposta.isPending}>
            Per camera specifica
          </ToggleButton>
        </ToggleButtonGroup>

        <TextField
          select
          label={ambito === 'camera' ? 'Camera' : 'Tipologia'}
          value={targetId}
          onChange={(e) => setTargetId(e.target.value)}
          required
          disabled={imposta.isPending}
        >
          {(ambito === 'camera' ? camere : tipologie).length === 0 && <MenuItem value="">Nessuna disponibile</MenuItem>}
          {ambito === 'camera'
            ? camere.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.nome}
                </MenuItem>
              ))
            : tipologie.map((t) => (
                <MenuItem key={t.id} value={t.id}>
                  {t.tipologiaCamera}
                </MenuItem>
              ))}
        </TextField>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Dal"
            type="date"
            value={dataInizio}
            onChange={(e) => setDataInizio(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={imposta.isPending}
          />
          <TextField
            label="Al"
            type="date"
            value={dataFine}
            onChange={(e) => setDataFine(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={imposta.isPending}
          />
        </Box>

        <TextField
          label="Prezzo per notte (€)"
          type="number"
          value={prezzoPerNotte}
          onChange={(e) => setPrezzoPerNotte(e.target.value)}
          disabled={imposta.isPending}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={imposta.isPending}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={imposta.isPending}>
          Salva periodo
        </Button>
      </DialogActions>
    </Dialog>
  )
}
