import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { useCreaEntrata, useAggiornaEntrata, type EntrataDto, type EntrataRequest } from '../api/entrate'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { CampoData } from './CampoData'

interface Props {
  strutturaId: string
  entrata: EntrataDto | null
  onClose: () => void
}

export function EntrataDialog({ strutturaId, entrata, onClose }: Props) {
  const [tipoEntrata, setTipoEntrata] = useState(entrata?.tipoEntrata ?? '')
  const [nome, setNome] = useState(entrata?.nome ?? '')
  const [importoEntrata, setImportoEntrata] = useState(String(entrata?.importoEntrata ?? ''))
  const [descrizione, setDescrizione] = useState(entrata?.descrizione ?? '')
  const [data, setData] = useState(formatoInputData(entrata?.data ? new Date(entrata.data) : new Date()))
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaEntrata(strutturaId)
  const aggiorna = useAggiornaEntrata(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '' || importoEntrata.trim() === '' || Number(importoEntrata) < 0) {
      setErrore('Nome e importo (non negativo) sono obbligatori.')
      return
    }
    setErrore(null)

    const request: EntrataRequest = {
      tipoEntrata: tipoEntrata.trim() === '' ? null : tipoEntrata.trim(),
      nome: nome.trim(),
      importoEntrata: Number(importoEntrata),
      descrizione: descrizione.trim() === '' ? null : descrizione.trim(),
      data: data === '' ? null : isoLocale(parsaInputData(data)),
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (entrata) {
      aggiorna.mutate({ entrataId: entrata.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{entrata ? 'Modifica entrata' : 'Nuova entrata'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required fullWidth disabled={inCorso} autoFocus />
          <TextField label="Importo (€)" type="number" value={importoEntrata} onChange={(e) => setImportoEntrata(e.target.value)} required fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Tipo entrata" value={tipoEntrata} onChange={(e) => setTipoEntrata(e.target.value)} fullWidth disabled={inCorso} />
          <CampoData label="Data" value={data} onChange={setData} fullWidth disabled={inCorso} />
        </Box>

        <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} multiline minRows={2} disabled={inCorso} />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {entrata ? 'Salva modifiche' : 'Crea entrata'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
