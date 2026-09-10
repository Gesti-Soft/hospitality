import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { StatoCamera, useAggiornaCamera, useCreaCamera, useCreaCamereNumerate, type CameraDto, type CameraRequest } from '../api/camere'
import type { TipologiaCameraDto } from '../api/tipologie'
import { useMobile } from '../lib/useMobile'

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
  const mobile = useMobile()
  const [nome, setNome] = useState(camera?.nome ?? '')
  const [tipologiaId, setTipologiaId] = useState(camera?.tipologiaId ?? tipologiaDiDefault ?? '')
  const [stateRoom, setStateRoom] = useState<StatoCamera>(camera?.stateRoom ?? StatoCamera.Pronta)
  const [capacitaOspiti, setCapacitaOspiti] = useState(camera?.capacitaOspiti != null ? String(camera.capacitaOspiti) : '')
  const [soggiornoMinimo, setSoggiornoMinimo] = useState(camera?.soggiornoMinimo != null ? String(camera.soggiornoMinimo) : '')
  // Solo in creazione: invece di un'unica camera, ne crea una sequenza numerata ("101".."110")
  // della stessa Tipologia/Stato/Capacità in un colpo solo — comodo per un pool di camere reali
  // identiche, evita di ripetere "Nuova camera" una per una.
  const [modalitaNumerate, setModalitaNumerate] = useState(false)
  const [prefisso, setPrefisso] = useState('')
  const [da, setDa] = useState('')
  const [a, setA] = useState('')
  const [errore, setErrore] = useState<string | null>(null)

  const daNumero = Number(da)
  const aNumero = Number(a)
  const quantitaNumerate =
    da.trim() !== '' && a.trim() !== '' && Number.isInteger(daNumero) && Number.isInteger(aNumero) && aNumero >= daNumero ? aNumero - daNumero + 1 : null

  const crea = useCreaCamera(strutturaId)
  const creaNumerate = useCreaCamereNumerate(strutturaId)
  const aggiorna = useAggiornaCamera(strutturaId)
  const inCorso = crea.isPending || creaNumerate.isPending || aggiorna.isPending

  function salva() {
    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (!camera && modalitaNumerate) {
      if (quantitaNumerate === null) {
        setErrore('Indica un intervallo valido (es. da 101 a 110).')
        return
      }
      setErrore(null)

      creaNumerate.mutate(
        {
          tipologiaId: tipologiaId === '' ? null : tipologiaId,
          stateRoom,
          prefisso: prefisso.trim() === '' ? null : prefisso.trim(),
          da: daNumero,
          a: aNumero,
          capacitaOspiti: capacitaOspiti.trim() === '' ? null : Number(capacitaOspiti),
          soggiornoMinimo: soggiornoMinimo.trim() === '' ? null : Number(soggiornoMinimo),
        },
        { onSuccess: onClose, onError },
      )
      return
    }

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
    }

    if (camera) {
      aggiorna.mutate({ cameraId: camera.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{camera ? 'Modifica camera' : 'Nuova camera'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        {!camera && (
          <FormControlLabel
            control={<Checkbox checked={modalitaNumerate} onChange={(e) => setModalitaNumerate(e.target.checked)} disabled={inCorso} />}
            label="Crea più camere numerate in sequenza"
          />
        )}

        {modalitaNumerate && !camera ? (
          <>
            <TextField
              label="Prefisso (opzionale)"
              value={prefisso}
              onChange={(e) => setPrefisso(e.target.value)}
              placeholder={'Es. "Camera " → Camera 101, Camera 102…'}
              disabled={inCorso}
              autoFocus
            />
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField
                label="Da numero"
                type="number"
                value={da}
                onChange={(e) => setDa(e.target.value)}
                required
                fullWidth
                disabled={inCorso}
              />
              <TextField label="A numero" type="number" value={a} onChange={(e) => setA(e.target.value)} required fullWidth disabled={inCorso} />
            </Box>
          </>
        ) : (
          <TextField label="Nome / numero camera" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />
        )}

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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {camera ? 'Salva modifiche' : modalitaNumerate ? `Crea ${quantitaNumerate ?? ''} camere`.trim() : 'Crea camera'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
