import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { StatoCamera, useAggiornaCamera, useCreaCamera, type CameraDto, type CameraRequest } from '../api/camere'
import type { TipologiaCameraDto } from '../api/tipologie'

const ETICHETTA_STATO: Record<StatoCamera, string> = {
  [StatoCamera.Pronta]: 'Pronta',
  [StatoCamera.Occupata]: 'Occupata',
  [StatoCamera.DaPulire]: 'Da pulire',
  [StatoCamera.NonDisponibile]: 'Non disponibile',
}

interface Props {
  strutturaId: string
  camera: CameraDto | null
  tipologie: TipologiaCameraDto[]
  tipologiaDiDefault?: string
  onClose: () => void
}

export function CameraDialog({ strutturaId, camera, tipologie, tipologiaDiDefault, onClose }: Props) {
  const [nome, setNome] = useState(camera?.nome ?? '')
  const [tipologiaId, setTipologiaId] = useState(camera?.tipologiaId ?? tipologiaDiDefault ?? '')
  const [stateRoom, setStateRoom] = useState<StatoCamera>(camera?.stateRoom ?? StatoCamera.Pronta)
  const [capacitaOspiti, setCapacitaOspiti] = useState(camera?.capacitaOspiti != null ? String(camera.capacitaOspiti) : '')
  const [soggiornoMinimo, setSoggiornoMinimo] = useState(camera?.soggiornoMinimo != null ? String(camera.soggiornoMinimo) : '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaCamera(strutturaId)
  const aggiorna = useAggiornaCamera(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome della camera è obbligatorio.')
      return
    }
    setErrore(null)

    const request: CameraRequest = {
      tipologiaId: tipologiaId === '' ? null : tipologiaId,
      stateRoom,
      nome: nome.trim(),
      capacitaOspiti: capacitaOspiti.trim() === '' ? null : Number(capacitaOspiti),
      soggiornoMinimo: soggiornoMinimo.trim() === '' ? null : Number(soggiornoMinimo),
      // Non editabili da questo dialog (v. "Impostazioni Wubook" nella pagina Wubook) — passati
      // invariati per non azzerarli ad ogni salvataggio della scheda camera principale.
      codiceCameraWubook: camera?.codiceCameraWubook ?? null,
      prezzoWubookOverride: camera?.prezzoWubookOverride ?? null,
      wubookSoloWoodoo: camera?.wubookSoloWoodoo ?? false,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (camera) {
      aggiorna.mutate({ cameraId: camera.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{camera ? 'Modifica camera' : 'Nuova camera'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <TextField label="Nome / numero camera" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        <TextField select label="Tipologia" value={tipologiaId} onChange={(e) => setTipologiaId(e.target.value)} disabled={inCorso}>
          <MenuItem value="">Nessuna tipologia</MenuItem>
          {tipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              {t.tipologiaCamera}
            </MenuItem>
          ))}
        </TextField>

        <TextField select label="Stato attuale" value={stateRoom} onChange={(e) => setStateRoom(Number(e.target.value) as StatoCamera)} disabled={inCorso}>
          {Object.entries(ETICHETTA_STATO).map(([valore, etichetta]) => (
            <MenuItem key={valore} value={valore}>
              {etichetta}
            </MenuItem>
          ))}
        </TextField>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Capacità ospiti"
            type="number"
            value={capacitaOspiti}
            onChange={(e) => setCapacitaOspiti(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 1 } }}
          />
          <TextField
            label="Soggiorno minimo (notti)"
            type="number"
            value={soggiornoMinimo}
            onChange={(e) => setSoggiornoMinimo(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 1 } }}
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {camera ? 'Salva modifiche' : 'Crea camera'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
