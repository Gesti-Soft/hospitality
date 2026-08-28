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
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { useAggiornaPianoRestrizione, useCreaPianoRestrizione, type PianoRestrizioneDto } from '../api/integrazioni'

interface Props {
  strutturaId: string
  piano: PianoRestrizioneDto | null
  onClose: () => void
}

export function PianoRestrizioneDialog({ strutturaId, piano, onClose }: Props) {
  const [nome, setNome] = useState(piano?.nome ?? '')
  const [minStay, setMinStay] = useState(piano?.regole?.minStay != null ? String(piano.regole.minStay) : '')
  const [maxStay, setMaxStay] = useState(piano?.regole?.maxStay != null ? String(piano.regole.maxStay) : '')
  const [chiuso, setChiuso] = useState(piano?.regole?.chiuso ?? false)
  const [chiusoArrivo, setChiusoArrivo] = useState(piano?.regole?.chiusoArrivo ?? false)
  const [chiusoPartenza, setChiusoPartenza] = useState(piano?.regole?.chiusoPartenza ?? false)
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaPianoRestrizione(strutturaId)
  const aggiorna = useAggiornaPianoRestrizione(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome è obbligatorio.')
      return
    }
    setErrore(null)

    const regole = {
      minStay: minStay.trim() === '' ? null : Number(minStay),
      minStayArrival: null,
      maxStay: maxStay.trim() === '' ? null : Number(maxStay),
      maxStayArrival: null,
      chiuso,
      chiusoArrivo,
      chiusoPartenza,
    }
    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (piano) {
      aggiorna.mutate({ pianoId: piano.id, request: { nome: nome.trim(), regole } }, { onSuccess: onClose, onError })
    } else {
      crea.mutate({ nome: nome.trim(), regole }, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{piano ? 'Modifica piano restrizione' : 'Nuovo piano restrizione'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Soggiorno minimo" type="number" value={minStay} onChange={(e) => setMinStay(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Soggiorno massimo" type="number" value={maxStay} onChange={(e) => setMaxStay(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <FormControlLabel control={<Checkbox checked={chiuso} onChange={(e) => setChiuso(e.target.checked)} disabled={inCorso} />} label="Chiuso (nessuna vendita)" />
        <FormControlLabel control={<Checkbox checked={chiusoArrivo} onChange={(e) => setChiusoArrivo(e.target.checked)} disabled={inCorso} />} label="Chiuso in arrivo (no check-in)" />
        <FormControlLabel control={<Checkbox checked={chiusoPartenza} onChange={(e) => setChiusoPartenza(e.target.checked)} disabled={inCorso} />} label="Chiuso in partenza (no check-out)" />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {piano ? 'Salva modifiche' : 'Crea piano'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
