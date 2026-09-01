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
import { useCreaPayTouristStruttura, useAggiornaPayTouristStruttura, type PayTouristStrutturaDto, type PayTouristStrutturaRequest } from '../api/integrazioni'
import type { TipologiaCameraDto } from '../api/tipologie'

interface Props {
  strutturaId: string
  strutturaPayTourist: PayTouristStrutturaDto | null
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function PayTouristStrutturaDialog({ strutturaId, strutturaPayTourist, tipologie, onClose }: Props) {
  const [nome, setNome] = useState(strutturaPayTourist?.nome ?? '')
  const [idStrutturaPaytourist, setIdStrutturaPaytourist] = useState(strutturaPayTourist?.idStrutturaPaytourist != null ? String(strutturaPayTourist.idStrutturaPaytourist) : '')
  const [tipologieIds, setTipologieIds] = useState<string[]>(strutturaPayTourist?.tipologieIds ?? [])
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaPayTouristStruttura(strutturaId)
  const aggiorna = useAggiornaPayTouristStruttura(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome è obbligatorio.')
      return
    }
    setErrore(null)

    const request: PayTouristStrutturaRequest = {
      nome: nome.trim(),
      idStrutturaPaytourist: idStrutturaPaytourist.trim() === '' ? null : Number(idStrutturaPaytourist),
      tipologieIds,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (strutturaPayTourist) {
      aggiorna.mutate({ payTouristStrutturaId: strutturaPayTourist.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{strutturaPayTourist ? 'Modifica struttura PayTourist' : 'Nuova struttura PayTourist'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />
        <TextField
          label="Id struttura PayTourist"
          type="number"
          value={idStrutturaPaytourist}
          onChange={(e) => setIdStrutturaPaytourist(e.target.value)}
          disabled={inCorso}
          helperText="structure_id fornito da PayTourist"
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
          {strutturaPayTourist ? 'Salva modifiche' : 'Crea struttura'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
