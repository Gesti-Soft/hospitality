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
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { useAggiornaCamera, useCamere, type CameraRequest } from '../api/camere'
import { useSincronizzaWubookCamera } from '../api/integrazioni'
import { fontMono, tokens } from '../theme'

interface Props {
  strutturaId: string
  cameraId: string
  cameraNome: string
  wubookAttiva: boolean
  onClose: () => void
}

export function ImpostazioniWubookCameraDialog({ strutturaId, cameraId, cameraNome, wubookAttiva, onClose }: Props) {
  const camere = useCamere(strutturaId)
  const camera = (camere.data ?? []).find((c) => c.id === cameraId) ?? null

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{wubookAttiva ? 'Modifica' : 'Crea'} camera su Wubook — {cameraNome}</DialogTitle>
      {camere.isLoading && (
        <DialogContent sx={{ pt: 1 }}>
          <Skeleton variant="rounded" height={160} />
        </DialogContent>
      )}
      {!camere.isLoading && camera && (
        <Form strutturaId={strutturaId} camera={camera} wubookAttiva={wubookAttiva} onClose={onClose} />
      )}
      {!camere.isLoading && !camera && (
        <DialogContent sx={{ pt: 1 }}>
          <Alert severity="error">Camera non trovata.</Alert>
        </DialogContent>
      )}
    </Dialog>
  )
}

function Form({
  strutturaId,
  camera,
  wubookAttiva,
  onClose,
}: {
  strutturaId: string
  camera: {
    id: string
    tipologiaId: string | null
    stateRoom: number
    nome: string
    capacitaOspiti: number | null
    soggiornoMinimo: number | null
    codiceCameraWubook: string | null
    prezzoWubookOverride: number | null
    wubookSoloWoodoo: boolean
  }
  wubookAttiva: boolean
  onClose: () => void
}) {
  const [codiceCameraWubook, setCodiceCameraWubook] = useState(camera.codiceCameraWubook ?? '')
  const [prezzoWubookOverride, setPrezzoWubookOverride] = useState(camera.prezzoWubookOverride != null ? String(camera.prezzoWubookOverride) : '')
  const [wubookSoloWoodoo, setWubookSoloWoodoo] = useState(camera.wubookSoloWoodoo)
  const [errore, setErrore] = useState<string | null>(null)

  const aggiorna = useAggiornaCamera(strutturaId)
  const sincronizza = useSincronizzaWubookCamera(strutturaId)
  const inCorso = aggiorna.isPending || sincronizza.isPending

  function salva() {
    setErrore(null)
    const request: CameraRequest = {
      tipologiaId: camera.tipologiaId,
      stateRoom: camera.stateRoom as CameraRequest['stateRoom'],
      nome: camera.nome,
      capacitaOspiti: camera.capacitaOspiti,
      soggiornoMinimo: camera.soggiornoMinimo,
      codiceCameraWubook: codiceCameraWubook.trim() === '' ? null : codiceCameraWubook.trim(),
      prezzoWubookOverride: prezzoWubookOverride.trim() === '' ? null : Number(prezzoWubookOverride),
      wubookSoloWoodoo,
    }
    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    // "Salva" replica il comportamento del legacy: prima persiste i campi in locale, poi crea/aggiorna
    // subito la camera su Wubook con quegli stessi valori — un solo click, non due azioni separate.
    aggiorna.mutate(
      { cameraId: camera.id, request },
      { onSuccess: () => sincronizza.mutate(camera.id, { onSuccess: onClose, onError }), onError },
    )
  }

  const woodooCambiato = wubookSoloWoodoo !== camera.wubookSoloWoodoo

  return (
    <>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          "Salva" {wubookAttiva ? 'aggiorna subito' : 'crea subito'} questa camera su Wubook con i valori qui sotto — se
          lasciati vuoti si usa il valore dedotto automaticamente (codice camera dal nome, prezzo dalla tipologia).
        </Typography>

        <TextField
          label="Codice camera (max 4)"
          value={codiceCameraWubook}
          onChange={(e) => setCodiceCameraWubook(e.target.value.toUpperCase().slice(0, 4))}
          disabled={inCorso}
          slotProps={{ htmlInput: { style: { fontFamily: fontMono } } }}
        />
        <TextField
          label="Prezzo (override tipologia)"
          type="number"
          value={prezzoWubookOverride}
          onChange={(e) => setPrezzoWubookOverride(e.target.value)}
          disabled={inCorso}
          slotProps={{ htmlInput: { min: 0 } }}
        />
        <Box>
          <FormControlLabel
            control={<Checkbox checked={wubookSoloWoodoo} onChange={(e) => setWubookSoloWoodoo(e.target.checked)} disabled={inCorso} />}
            label="WooDoo CM only (non vendibile sul motore Wubook)"
          />
          {woodooCambiato && wubookAttiva && (
            <Alert severity="info" sx={{ mt: 1 }}>
              Questa camera è già associata a Wubook: il cambio si applica solo alla creazione — per renderlo effettivo,
              rimuovi l'associazione e sincronizza di nuovo.
            </Alert>
          )}
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {inCorso ? 'Salvataggio…' : wubookAttiva ? 'Salva e aggiorna su Wubook' : 'Salva e crea su Wubook'}
        </Button>
      </DialogActions>
    </>
  )
}
