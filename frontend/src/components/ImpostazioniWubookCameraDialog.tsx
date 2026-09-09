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
import { useCamereRemoteWubook, useSincronizzaWubookCamera } from '../api/integrazioni'
import { useTipologie } from '../api/tipologie'
import { fontMono, tokens } from '../theme'

interface Props {
  strutturaId: string
  cameraId: string
  cameraNome: string
  wubookAttiva: boolean
  idCameraWubook: number | null
  onClose: () => void
}

/** Stesso algoritmo di GestiSoft.Application.Wubook.WubookCamereService.ShortNameDa (backend) — solo per una camera MAI creata su OTA: è il valore che verrebbe inviato se il campo resta invariato. Per una camera già associata non basta (il codice reale può essere stato scelto a mano, es. dal legacy, e non seguire questo algoritmo) — in quel caso il valore reale si legge da OTA stessa, vedi sotto. */
function codiceDedottoDaNome(nome: string): string {
  const alfanumerico = nome.replace(/[^a-zA-Z0-9]/g, '').toUpperCase()
  if (alfanumerico.length === 0) return 'ROOM'
  return alfanumerico.length <= 4 ? alfanumerico.padEnd(4, 'X') : alfanumerico.slice(0, 4)
}

export function ImpostazioniWubookCameraDialog({ strutturaId, cameraId, cameraNome, wubookAttiva, idCameraWubook, onClose }: Props) {
  const camere = useCamere(strutturaId)
  const tipologie = useTipologie(strutturaId)
  // Per una camera già associata, il codice/prezzo "attualmente in uso" è quello registrato SU OTA
  // — un valore storico, magari scelto a mano, che il nostro algoritmo di deduzione non può
  // indovinare. Va letto da OTA stessa (fetch_rooms), non calcolato qui.
  const remoto = useCamereRemoteWubook(strutturaId, wubookAttiva)
  const camera = (camere.data ?? []).find((c) => c.id === cameraId) ?? null
  const tipologia = camera ? (tipologie.data ?? []).find((t) => t.id === camera.tipologiaId) ?? null : null
  const cameraRemota = wubookAttiva && idCameraWubook != null ? (remoto.data ?? []).find((r) => r.id === idCameraWubook) ?? null : null
  const caricamento = camere.isLoading || (wubookAttiva && remoto.isLoading)

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{wubookAttiva ? 'Modifica' : 'Crea'} camera su OTA — {cameraNome}</DialogTitle>
      {caricamento && (
        <DialogContent sx={{ pt: 1 }}>
          <Skeleton variant="rounded" height={160} />
        </DialogContent>
      )}
      {!caricamento && camera && (
        <Form strutturaId={strutturaId} camera={camera} tipologia={tipologia} cameraRemota={cameraRemota} wubookAttiva={wubookAttiva} onClose={onClose} />
      )}
      {!caricamento && !camera && (
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
  tipologia,
  cameraRemota,
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
  tipologia: { tipologiaCamera: string; prezzoDefault: number | null } | null
  cameraRemota: { shortName: string | null; prezzo: number } | null
  wubookAttiva: boolean
  onClose: () => void
}) {
  // Il campo parte già valorizzato con quello che è realmente in uso oggi: l'override esplicito se
  // c'è; altrimenti, se la camera è già associata, il valore letto da OTA (l'unico affidabile per
  // una camera creata prima o a mano); altrimenti il valore che verrebbe dedotto alla creazione —
  // mai una casella vuota che nasconde il dato reale.
  const [codiceCameraWubook, setCodiceCameraWubook] = useState(
    camera.codiceCameraWubook ?? cameraRemota?.shortName ?? codiceDedottoDaNome(camera.nome),
  )
  const [prezzoWubookOverride, setPrezzoWubookOverride] = useState(String(camera.prezzoWubookOverride ?? cameraRemota?.prezzo ?? tipologia?.prezzoDefault ?? 0))
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
        {wubookAttiva && !cameraRemota && (
          <Alert severity="warning">
            Non è stato possibile leggere i valori attuali da OTA — quelli qui sotto sono solo una stima, verificali prima di salvare.
          </Alert>
        )}
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          "Salva" {wubookAttiva ? 'aggiorna subito' : 'crea subito'} questa camera su OTA con i valori qui sotto —
          già precompilati con quelli attualmente in uso.
        </Typography>

        <TextField
          label="Codice camera (max 4)"
          value={codiceCameraWubook}
          onChange={(e) => setCodiceCameraWubook(e.target.value.toUpperCase().slice(0, 4))}
          disabled={inCorso}
          slotProps={{ htmlInput: { style: { fontFamily: fontMono } } }}
        />
        <TextField
          label="Prezzo"
          type="number"
          value={prezzoWubookOverride}
          onChange={(e) => setPrezzoWubookOverride(e.target.value)}
          disabled={inCorso}
          slotProps={{ htmlInput: { min: 0 } }}
        />
        <Box>
          <FormControlLabel
            control={<Checkbox checked={wubookSoloWoodoo} onChange={(e) => setWubookSoloWoodoo(e.target.checked)} disabled={inCorso} />}
            label="OTA CM only"
          />
          {woodooCambiato && wubookAttiva && (
            <Alert severity="info" sx={{ mt: 1 }}>
              Questa camera è già associata: il cambio si applica solo alla creazione — per renderlo effettivo,
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
          {inCorso ? 'Salvataggio…' : wubookAttiva ? 'Salva e aggiorna' : 'Salva e crea'}
        </Button>
      </DialogActions>
    </>
  )
}
