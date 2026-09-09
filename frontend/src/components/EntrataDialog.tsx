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
import { useCamere } from '../api/camere'
import { useTipologie } from '../api/tipologie'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { SelettoreTipoConCamera } from './finanze/FinanzeComuni'

interface Props {
  strutturaId: string
  entrata: EntrataDto | null
  onClose: () => void
}

export function EntrataDialog({ strutturaId, entrata, onClose }: Props) {
  const mobile = useMobile()
  const [tipoEntrata, setTipoEntrata] = useState(entrata?.tipoEntrata ?? '')
  const [importoEntrata, setImportoEntrata] = useState(String(entrata?.importoEntrata ?? ''))
  const [descrizione, setDescrizione] = useState(entrata?.descrizione ?? '')
  const [data, setData] = useState(formatoInputData(entrata?.data ? new Date(entrata.data) : new Date()))
  const [errore, setErrore] = useState<string | null>(null)

  const tipologie = useTipologie(strutturaId)
  const camere = useCamere(strutturaId)
  const listaTipologie = tipologie.data ?? []
  const listaCamere = camere.data ?? []

  const crea = useCreaEntrata(strutturaId)
  const aggiorna = useAggiornaEntrata(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (descrizione.trim() === '' || importoEntrata.trim() === '' || Number(importoEntrata) < 0) {
      setErrore('Descrizione e importo (non negativo) sono obbligatori.')
      return
    }
    setErrore(null)

    const request: EntrataRequest = {
      tipoEntrata: tipoEntrata.trim() === '' ? null : tipoEntrata.trim(),
      nome: null,
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{entrata ? 'Modifica entrata' : 'Nuova entrata'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} required fullWidth disabled={inCorso} autoFocus />
          <TextField label="Importo (€)" type="number" value={importoEntrata} onChange={(e) => setImportoEntrata(e.target.value)} required fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <SelettoreTipoConCamera label="Tipo entrata" valore={tipoEntrata} onChange={setTipoEntrata} tipologie={listaTipologie} camere={listaCamere} disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <CampoData label="Data" value={data} onChange={setData} fullWidth disabled={inCorso} />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {entrata ? 'Salva modifiche' : 'Crea entrata'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
