import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
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
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'

interface Props {
  strutturaId: string
  camere: CameraDto[]
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function PrezzoDialog({ strutturaId, camere, tipologie, onClose }: Props) {
  const mobile = useMobile()
  // La Tipologia va sempre scelta per prima (richiesta esplicita): "per camera specifica" filtra
  // solo le camere di quella tipologia, non elenca più tutte le camere della struttura insieme.
  const [tipologiaId, setTipologiaId] = useState('')
  const [ambito, setAmbito] = useState<'tipologia' | 'camera'>('tipologia')
  const [cameraId, setCameraId] = useState('')
  const [dataInizio, setDataInizio] = useState(formatoInputData(new Date()))
  const [dataFine, setDataFine] = useState(formatoInputData(new Date()))
  const [prezzoPerNotte, setPrezzoPerNotte] = useState('')
  const [errore, setErrore] = useState<string | null>(null)

  const imposta = useImpostaPrezzo(strutturaId)

  const camereTipologia = camere.filter((c) => c.tipologiaId === tipologiaId)

  function salva() {
    const inizio = parsaInputData(dataInizio)
    const fine = parsaInputData(dataFine)

    if (tipologiaId === '') {
      setErrore('Seleziona una tipologia.')
      return
    }
    if (ambito === 'camera' && cameraId === '') {
      setErrore('Seleziona una camera.')
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
      cameraId: ambito === 'camera' ? cameraId : null,
      tipologiaId: ambito === 'tipologia' ? tipologiaId : null,
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>Nuovo periodo di prezzo</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField
          select
          label="Tipologia"
          value={tipologiaId}
          onChange={(e) => {
            setTipologiaId(e.target.value)
            setCameraId('')
          }}
          required
          disabled={imposta.isPending}
        >
          {tipologie.length === 0 && <MenuItem value="">Nessuna tipologia disponibile</MenuItem>}
          {tipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              {t.tipologiaCamera}
            </MenuItem>
          ))}
        </TextField>

        <ToggleButtonGroup
          exclusive
          value={ambito}
          onChange={(_, valore) => {
            if (valore) setAmbito(valore)
          }}
          size="small"
        >
          <ToggleButton value="tipologia" disabled={imposta.isPending}>
            Tutta la tipologia
          </ToggleButton>
          <ToggleButton value="camera" disabled={imposta.isPending || tipologiaId === ''}>
            Camera specifica
          </ToggleButton>
        </ToggleButtonGroup>

        {ambito === 'camera' && (
          <Autocomplete
            options={camereTipologia}
            getOptionLabel={(c) => c.nome}
            value={camereTipologia.find((c) => c.id === cameraId) ?? null}
            onChange={(_, valore) => setCameraId(valore?.id ?? '')}
            disabled={imposta.isPending}
            noOptionsText="Nessuna camera per questa tipologia"
            renderInput={(params) => <TextField {...params} label="Camera" required placeholder="Cerca per nome…" />}
          />
        )}

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <CampoData label="Dal" value={dataInizio} onChange={setDataInizio} fullWidth disabled={imposta.isPending} />
          <CampoData label="Al" value={dataFine} onChange={setDataFine} min={dataInizio || undefined} fullWidth disabled={imposta.isPending} />
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
        <Button variant="contained" color="primary" onClick={salva} disabled={imposta.isPending}>
          Salva periodo
        </Button>
      </DialogActions>
    </Dialog>
  )
}
