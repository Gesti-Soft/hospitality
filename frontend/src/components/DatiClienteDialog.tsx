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
import { useCreaDatiCliente, useAggiornaDatiCliente, type DatiClienteDto, type DatiClienteRequest } from '../api/fatturazione'

interface Props {
  strutturaId: string
  cliente: DatiClienteDto | null
  onClose: () => void
}

export function DatiClienteDialog({ strutturaId, cliente, onClose }: Props) {
  const [denominazione, setDenominazione] = useState(cliente?.denominazione ?? '')
  const [nome, setNome] = useState(cliente?.nome ?? '')
  const [cognome, setCognome] = useState(cliente?.cognome ?? '')
  const [pIva, setPIva] = useState(cliente?.pIva ?? '')
  const [codiceFiscale, setCodiceFiscale] = useState(cliente?.codiceFiscale ?? '')
  const [indirizzo, setIndirizzo] = useState(cliente?.indirizzo ?? '')
  const [nCivico, setNCivico] = useState(cliente?.nCivico ?? '')
  const [cap, setCap] = useState(cliente?.cap ?? '')
  const [luogoResidenza, setLuogoResidenza] = useState(cliente?.luogoResidenza ?? '')
  const [provincia, setProvincia] = useState(cliente?.provincia ?? '')
  const [cittadinanza, setCittadinanza] = useState(cliente?.cittadinanza ?? '')
  const [iso2, setIso2] = useState(cliente?.iso2 ?? '')
  const [codiceDestinatario, setCodiceDestinatario] = useState(cliente?.codiceDestinatario ?? '')
  const [pec, setPec] = useState(cliente?.pec ?? '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaDatiCliente(strutturaId)
  const aggiorna = useAggiornaDatiCliente(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (denominazione.trim() === '' && (nome.trim() === '' || cognome.trim() === '')) {
      setErrore('Indica una denominazione (azienda) oppure nome e cognome (privato).')
      return
    }
    setErrore(null)

    const vuoto = (v: string) => (v.trim() === '' ? null : v.trim())
    const request: DatiClienteRequest = {
      denominazione: vuoto(denominazione),
      nome: vuoto(nome),
      cognome: vuoto(cognome),
      pIva: vuoto(pIva),
      codiceFiscale: vuoto(codiceFiscale),
      indirizzo: vuoto(indirizzo),
      nCivico: vuoto(nCivico),
      cap: vuoto(cap),
      luogoResidenza: vuoto(luogoResidenza),
      provincia: vuoto(provincia),
      cittadinanza: vuoto(cittadinanza),
      iso2: vuoto(iso2),
      codiceDestinatario: vuoto(codiceDestinatario),
      pec: vuoto(pec),
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (cliente) {
      aggiorna.mutate({ clienteId: cliente.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{cliente ? 'Modifica cliente' : 'Nuovo cliente'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Denominazione (se azienda)" value={denominazione} onChange={(e) => setDenominazione(e.target.value)} disabled={inCorso} autoFocus />

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Partita IVA" value={pIva} onChange={(e) => setPIva(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Codice fiscale" value={codiceFiscale} onChange={(e) => setCodiceFiscale(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Indirizzo" value={indirizzo} onChange={(e) => setIndirizzo(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="N. civico" value={nCivico} onChange={(e) => setNCivico(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
          <TextField label="CAP" value={cap} onChange={(e) => setCap(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Comune di residenza" value={luogoResidenza} onChange={(e) => setLuogoResidenza(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Provincia" value={provincia} onChange={(e) => setProvincia(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
          <TextField label="Cittadinanza" value={cittadinanza} onChange={(e) => setCittadinanza(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nazione (ISO2)" value={iso2} onChange={(e) => setIso2(e.target.value)} sx={{ width: 130 }} disabled={inCorso} />
          <TextField label="Codice destinatario SDI" value={codiceDestinatario} onChange={(e) => setCodiceDestinatario(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="PEC" value={pec} onChange={(e) => setPec(e.target.value)} fullWidth disabled={inCorso} />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {cliente ? 'Salva modifiche' : 'Crea cliente'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
