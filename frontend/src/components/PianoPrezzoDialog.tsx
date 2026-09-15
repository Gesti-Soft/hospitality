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
import {
  TIPO_VARIAZIONE_IMPORTO,
  TIPO_VARIAZIONE_PERCENTUALE,
  useAggiornaPianoPrezzo,
  useCreaPianoPrezzo,
  type PianoPrezzoDto,
} from '../api/integrazioni'
import { useMobile } from '../lib/useMobile'

interface Props {
  strutturaId: string
  piano: PianoPrezzoDto | null
  piani: PianoPrezzoDto[]
  onClose: () => void
}

export function PianoPrezzoDialog({ strutturaId, piano, piani, onClose }: Props) {
  const mobile = useMobile()
  const [nome, setNome] = useState(piano?.nome ?? '')
  const [parentId, setParentId] = useState(piano?.parentId != null ? String(piano.parentId) : '0')
  const [tipoVariazione, setTipoVariazione] = useState(
    piano?.tipoVariazione != null ? String(piano.tipoVariazione) : String(TIPO_VARIAZIONE_PERCENTUALE),
  )
  const [variazione, setVariazione] = useState(piano?.variazione != null ? String(piano.variazione) : '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaPianoPrezzo(strutturaId)
  const aggiorna = useAggiornaPianoPrezzo(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome è obbligatorio.')
      return
    }
    if (variazione.trim() === '') {
      setErrore('Indica la variazione di prezzo rispetto al piano di partenza.')
      return
    }
    setErrore(null)
    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (piano) {
      aggiorna.mutate(
        { pianoId: piano.id, request: { nome: nome.trim(), tipoVariazione: Number(tipoVariazione), variazione: Number(variazione) } },
        { onSuccess: onClose, onError },
      )
    } else {
      crea.mutate(
        { nome: nome.trim(), parentId: Number(parentId), tipoVariazione: Number(tipoVariazione), variazione: Number(variazione) },
        { onSuccess: onClose, onError },
      )
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{piano ? 'Modifica piano prezzo' : 'Nuovo piano prezzo'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        {!piano && (
          <TextField select label="Piano di partenza" value={parentId} onChange={(e) => setParentId(e.target.value)} disabled={inCorso} helperText="Di solito il piano 0 (Parity)">
            <MenuItem value="0">Parity (0)</MenuItem>
            {piani.filter((p) => !p.isVirtual).map((p) => (
              <MenuItem key={p.id} value={p.id}>
                {p.nome} ({p.id})
              </MenuItem>
            ))}
          </TextField>
        )}

        <TextField select label="Tipo variazione" value={tipoVariazione} onChange={(e) => setTipoVariazione(e.target.value)} disabled={inCorso}>
          <MenuItem value={String(TIPO_VARIAZIONE_PERCENTUALE)}>Percentuale (%)</MenuItem>
          <MenuItem value={String(TIPO_VARIAZIONE_IMPORTO)}>Importo fisso (€)</MenuItem>
        </TextField>

        <TextField
          label="Variazione"
          type="number"
          value={variazione}
          onChange={(e) => setVariazione(e.target.value)}
          disabled={inCorso}
          helperText="Positiva o negativa, rispetto al piano di partenza"
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {piano ? 'Salva modifiche' : 'Crea piano'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
