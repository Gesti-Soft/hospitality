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
import { useCreaSpesa, useAggiornaSpesa, type SpesaDto, type SpesaRequest } from '../api/spese'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'

interface Props {
  strutturaId: string
  spesa: SpesaDto | null
  onClose: () => void
}

export function SpesaDialog({ strutturaId, spesa, onClose }: Props) {
  const [tipoSpesa, setTipoSpesa] = useState(spesa?.tipoSpesa ?? '')
  const [nome, setNome] = useState(spesa?.nome ?? '')
  const [importoSpesa, setImportoSpesa] = useState(String(spesa?.importoSpesa ?? ''))
  const [descrizione, setDescrizione] = useState(spesa?.descrizione ?? '')
  const [metodoPagamento, setMetodoPagamento] = useState(spesa?.metodoPagamento ?? '')
  const [dataSpesa, setDataSpesa] = useState(formatoInputData(spesa?.dataSpesa ? new Date(spesa.dataSpesa) : new Date()))
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaSpesa(strutturaId)
  const aggiorna = useAggiornaSpesa(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (nome.trim() === '' || importoSpesa.trim() === '' || Number(importoSpesa) < 0) {
      setErrore('Nome e importo (non negativo) sono obbligatori.')
      return
    }
    setErrore(null)

    const request: SpesaRequest = {
      tipoSpesa: tipoSpesa.trim() === '' ? null : tipoSpesa.trim(),
      nome: nome.trim(),
      importoSpesa: Number(importoSpesa),
      descrizione: descrizione.trim() === '' ? null : descrizione.trim(),
      metodoPagamento: metodoPagamento.trim() === '' ? null : metodoPagamento.trim(),
      dataSpesa: dataSpesa === '' ? null : isoLocale(parsaInputData(dataSpesa)),
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (spesa) {
      aggiorna.mutate({ spesaId: spesa.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{spesa ? 'Modifica spesa' : 'Nuova spesa'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required fullWidth disabled={inCorso} autoFocus />
          <TextField label="Importo (€)" type="number" value={importoSpesa} onChange={(e) => setImportoSpesa(e.target.value)} required fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Tipo spesa" value={tipoSpesa} onChange={(e) => setTipoSpesa(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Metodo di pagamento" value={metodoPagamento} onChange={(e) => setMetodoPagamento(e.target.value)} fullWidth disabled={inCorso} />
          <TextField
            label="Data"
            type="date"
            value={dataSpesa}
            onChange={(e) => setDataSpesa(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={inCorso}
          />
        </Box>

        <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} multiline minRows={2} disabled={inCorso} />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {spesa ? 'Salva modifiche' : 'Crea spesa'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
