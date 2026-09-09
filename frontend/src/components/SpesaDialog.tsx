import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import { useCreaSpesa, useAggiornaSpesa, type SpesaDto, type SpesaRequest } from '../api/spese'
import { useCamere } from '../api/camere'
import { useTipologie } from '../api/tipologie'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { SelettoreTipoConCamera } from './finanze/FinanzeComuni'

const OPZIONI_METODO_PAGAMENTO = ['Contanti', 'Bonifico bancario', 'Carta di credito', 'POS', 'Assegno']

interface Props {
  strutturaId: string
  spesa: SpesaDto | null
  onClose: () => void
}

export function SpesaDialog({ strutturaId, spesa, onClose }: Props) {
  const mobile = useMobile()
  const [tipoSpesa, setTipoSpesa] = useState(spesa?.tipoSpesa ?? '')
  const [importoSpesa, setImportoSpesa] = useState(String(spesa?.importoSpesa ?? ''))
  const [descrizione, setDescrizione] = useState(spesa?.descrizione ?? '')
  const [metodoPagamento, setMetodoPagamento] = useState(spesa?.metodoPagamento ?? '')
  const [dataSpesa, setDataSpesa] = useState(formatoInputData(spesa?.dataSpesa ? new Date(spesa.dataSpesa) : new Date()))
  const [errore, setErrore] = useState<string | null>(null)

  const tipologie = useTipologie(strutturaId)
  const camere = useCamere(strutturaId)
  const listaTipologie = tipologie.data ?? []
  const listaCamere = camere.data ?? []

  const crea = useCreaSpesa(strutturaId)
  const aggiorna = useAggiornaSpesa(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (descrizione.trim() === '' || importoSpesa.trim() === '' || Number(importoSpesa) < 0) {
      setErrore('Descrizione e importo (non negativo) sono obbligatori.')
      return
    }
    setErrore(null)

    const request: SpesaRequest = {
      tipoSpesa: tipoSpesa.trim() === '' ? null : tipoSpesa.trim(),
      nome: null,
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{spesa ? 'Modifica spesa' : 'Nuova spesa'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} required fullWidth disabled={inCorso} autoFocus />
          <TextField label="Importo (€)" type="number" value={importoSpesa} onChange={(e) => setImportoSpesa(e.target.value)} required fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <SelettoreTipoConCamera label="Tipo spesa" valore={tipoSpesa} onChange={setTipoSpesa} tipologie={listaTipologie} camere={listaCamere} disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <Autocomplete
            freeSolo
            forcePopupIcon
            fullWidth
            disabled={inCorso}
            options={OPZIONI_METODO_PAGAMENTO}
            inputValue={metodoPagamento}
            onInputChange={(_, valore) => setMetodoPagamento(valore)}
            renderInput={(params) => <TextField {...params} label="Metodo di pagamento" />}
          />
          <CampoData label="Data" value={dataSpesa} onChange={setDataSpesa} fullWidth disabled={inCorso} />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {spesa ? 'Salva modifiche' : 'Crea spesa'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
