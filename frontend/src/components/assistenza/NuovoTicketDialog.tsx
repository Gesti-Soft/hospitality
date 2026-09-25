import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../../api/client'
import { OGGETTO_MAX_CARATTERI, TESTO_MAX_CARATTERI, useApriTicket, type TicketDettaglioDto } from '../../api/assistenza'
import { tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { useMobile } from '../../lib/useMobile'
import { SelettoreFoto } from './SelettoreFoto'

export function NuovoTicketDialog({
  strutturaId,
  onClose,
  onAperto,
}: {
  strutturaId: string
  onClose: () => void
  onAperto: (dettaglio: TicketDettaglioDto) => void
}) {
  const mobile = useMobile()
  const toast = useToast()
  const apri = useApriTicket(strutturaId)
  const [oggetto, setOggetto] = useState('')
  const [testo, setTesto] = useState('')
  const [foto, setFoto] = useState<File[]>([])

  const completo = oggetto.trim().length > 0 && testo.trim().length > 0

  function invia() {
    apri.mutate(
      { oggetto: oggetto.trim(), testo: testo.trim(), allegati: foto },
      {
        onSuccess: (dettaglio) => {
          toast.successo(`Ticket n. ${dettaglio.ticket.numero} inviato all'assistenza.`)
          onAperto(dettaglio)
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Invio non riuscito, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={apri.isPending ? undefined : onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>Nuovo ticket di assistenza</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Descrivi il problema: cosa stavi facendo, cosa ti aspettavi e cosa è successo. Uno screenshot aiuta molto.
          Ti risponderemo qui, e al titolare dell&apos;account arriverà anche un&apos;email.
        </Typography>
        <TextField
          label="Oggetto"
          placeholder="Es. Non riesco a stampare la fattura"
          value={oggetto}
          onChange={(e) => setOggetto(e.target.value)}
          disabled={apri.isPending}
          autoFocus
          slotProps={{ htmlInput: { maxLength: OGGETTO_MAX_CARATTERI } }}
        />
        <TextField
          label="Messaggio"
          multiline
          minRows={5}
          maxRows={14}
          value={testo}
          onChange={(e) => setTesto(e.target.value)}
          disabled={apri.isPending}
          slotProps={{ htmlInput: { maxLength: TESTO_MAX_CARATTERI } }}
        />
        <Box>
          <SelettoreFoto foto={foto} onChange={setFoto} disabilitato={apri.isPending} />
        </Box>
        <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary }}>
          Se lo screenshot mostra dati degli ospiti (documenti, recapiti), copri la parte che non serve prima di allegarlo.
        </Typography>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={apri.isPending}>
          Annulla
        </Button>
        <Button variant="contained" onClick={invia} disabled={!completo || apri.isPending}>
          {apri.isPending ? 'Invio…' : 'Invia ticket'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
