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
import { useCreaTipologia, useAggiornaTipologia, useAggiornaPulizieTipologia, type TipologiaCameraDto, type TipologiaCameraRequest } from '../api/tipologie'
import { useMobile } from '../lib/useMobile'

interface Props {
  strutturaId: string
  tipologia: TipologiaCameraDto | null
  onClose: () => void
}

export function TipologiaDialog({ strutturaId, tipologia, onClose }: Props) {
  const mobile = useMobile()
  const [nome, setNome] = useState(tipologia?.tipologiaCamera ?? '')
  const [prezzoDefault, setPrezzoDefault] = useState(tipologia?.prezzoDefault != null ? String(tipologia.prezzoDefault) : '')
  const [numeroImplementoPersona, setNumeroImplementoPersona] = useState(String(tipologia?.numeroImplementoPersona ?? 2))
  const [implemento, setImplemento] = useState(String(tipologia?.implemento ?? 0))
  const [spesePulizia, setSpesePulizia] = useState(tipologia?.spesePulizia != null ? String(tipologia.spesePulizia) : '')
  const [animali, setAnimali] = useState(tipologia?.animali != null ? String(tipologia.animali) : '')
  const [cauzione, setCauzione] = useState(tipologia?.cauzione != null ? String(tipologia.cauzione) : '')
  // Vuoto = come la struttura, 0 = nessuna, N = ogni N giorni.
  const [intervalloPulizia, setIntervalloPulizia] = useState(tipologia?.intervalloPuliziaGiorni != null ? String(tipologia.intervalloPuliziaGiorni) : '')
  const [intervalloBiancheria, setIntervalloBiancheria] = useState(tipologia?.intervalloBiancheriaGiorni != null ? String(tipologia.intervalloBiancheriaGiorni) : '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaTipologia(strutturaId)
  const aggiorna = useAggiornaTipologia(strutturaId)
  const aggiornaPulizie = useAggiornaPulizieTipologia(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending || aggiornaPulizie.isPending

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome della tipologia è obbligatorio.')
      return
    }
    setErrore(null)

    const request: TipologiaCameraRequest = {
      tipologiaCamera: nome.trim(),
      prezzoDefault: prezzoDefault.trim() === '' ? null : Number(prezzoDefault),
      numeroImplementoPersona: Number(numeroImplementoPersona) || 0,
      implemento: Number(implemento) || 0,
      spesePulizia: spesePulizia.trim() === '' ? null : Number(spesePulizia),
      animali: animali.trim() === '' ? null : Number(animali),
      cauzione: cauzione.trim() === '' ? null : Number(cauzione),
      // Non editabili da questo dialog (v. il dialog camera della pagina Servizi OTA) — passati
      // invariati per non azzerarli ad ogni salvataggio della scheda tipologia principale.
      codiceCameraWubook: tipologia?.codiceCameraWubook ?? null,
      wubookSoloWoodoo: tipologia?.wubookSoloWoodoo ?? false,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    // Le frequenze delle pulizie hanno un endpoint loro (vedi useAggiornaPulizieTipologia): si salvano
    // dopo la tipologia, sull'id che la tipologia ha appena ricevuto o che aveva già.
    const salvaPulizie = (tipologiaId: string) =>
      aggiornaPulizie.mutate(
        {
          tipologiaId,
          intervalloPuliziaGiorni: intervalloPulizia.trim() === '' ? null : Number(intervalloPulizia),
          intervalloBiancheriaGiorni: intervalloBiancheria.trim() === '' ? null : Number(intervalloBiancheria),
        },
        { onSuccess: onClose, onError },
      )

    if (tipologia) {
      aggiorna.mutate({ tipologiaId: tipologia.id, request }, { onSuccess: () => salvaPulizie(tipologia.id), onError })
    } else {
      crea.mutate(request, { onSuccess: (creata) => salvaPulizie(creata.id), onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{tipologia ? 'Modifica tipologia' : 'Nuova tipologia'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Nome tipologia" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Prezzo default (€/notte)" type="number" value={prezzoDefault} onChange={(e) => setPrezzoDefault(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Cauzione (€)" type="number" value={cauzione} onChange={(e) => setCauzione(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField
            label="Ospiti inclusi nel prezzo"
            type="number"
            value={numeroImplementoPersona}
            onChange={(e) => setNumeroImplementoPersona(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0 } }}
            helperText="Sopra questa soglia si applica il supplemento per notte"
          />
          <TextField label="Supplemento per persona extra (€/notte)" type="number" value={implemento} onChange={(e) => setImplemento(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Spese pulizia (€)" type="number" value={spesePulizia} onChange={(e) => setSpesePulizia(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Supplemento animali (€)" type="number" value={animali} onChange={(e) => setAnimali(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField
            label="Pulizia durante il soggiorno ogni (giorni)"
            type="number"
            value={intervalloPulizia}
            onChange={(e) => setIntervalloPulizia(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0, max: 30 } }}
            helperText="Vuoto = come la struttura. 0 = nessuna"
          />
          <TextField
            label="Cambio biancheria ogni (giorni)"
            type="number"
            value={intervalloBiancheria}
            onChange={(e) => setIntervalloBiancheria(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0, max: 30 } }}
            helperText="Vuoto = come la struttura. 0 = nessuno"
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {tipologia ? 'Salva modifiche' : 'Crea tipologia'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
