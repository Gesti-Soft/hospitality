import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import ListItemText from '@mui/material/ListItemText'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { useCreaOsservatorioAppartamento, useAggiornaOsservatorioAppartamento, type OsservatorioAppartamentoDto, type OsservatorioAppartamentoRequest } from '../api/integrazioni'
import type { TipologiaCameraDto } from '../api/tipologie'

interface Props {
  strutturaId: string
  appartamento: OsservatorioAppartamentoDto | null
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function OsservatorioAppartamentoDialog({ strutturaId, appartamento, tipologie, onClose }: Props) {
  const [nome, setNome] = useState(appartamento?.nome ?? '')
  const [entityCode, setEntityCode] = useState(appartamento?.entityCode ?? '')
  const [password, setPassword] = useState('')
  const [hotelCode, setHotelCode] = useState(appartamento?.hotelCode ?? '')
  const [tipologieIds, setTipologieIds] = useState<string[]>(appartamento?.tipologieIds ?? [])
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaOsservatorioAppartamento(strutturaId)
  const aggiorna = useAggiornaOsservatorioAppartamento(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome dell\'appartamento è obbligatorio.')
      return
    }
    setErrore(null)

    const request: OsservatorioAppartamentoRequest = {
      nome: nome.trim(),
      entityCode: entityCode.trim() === '' ? null : entityCode.trim(),
      password: password.trim() === '' ? null : password.trim(),
      hotelCode: hotelCode.trim() === '' ? null : hotelCode.trim(),
      tipologieIds,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (appartamento) {
      aggiorna.mutate({ appartamentoId: appartamento.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{appartamento ? 'Modifica appartamento' : 'Nuovo appartamento'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Entity code" value={entityCode} onChange={(e) => setEntityCode(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Hotel code" value={hotelCode} onChange={(e) => setHotelCode(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <TextField
          label="Password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          disabled={inCorso}
          helperText={appartamento?.credenzialiConfigurate ? "Già salvata: lasciarla vuota e salvare la AZZERA" : ' '}
        />

        <TextField
          select
          label="Tipologie camera instradate"
          value={tipologieIds}
          onChange={(e) => setTipologieIds(typeof e.target.value === 'string' ? e.target.value.split(',') : (e.target.value as string[]))}
          slotProps={{ select: { multiple: true, renderValue: (selected) => (selected as string[]).length + ' selezionate' } }}
          disabled={inCorso}
        >
          {tipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              <Checkbox checked={tipologieIds.includes(t.id)} size="small" />
              <ListItemText primary={t.tipologiaCamera} />
            </MenuItem>
          ))}
        </TextField>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {appartamento ? 'Salva modifiche' : 'Crea appartamento'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
