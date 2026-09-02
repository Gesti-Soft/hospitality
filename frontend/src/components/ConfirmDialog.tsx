import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'

interface Props {
  titolo: string
  messaggio: string
  testoConferma?: string
  pericoloso?: boolean
  inCorso?: boolean
  onConferma: () => void
  onAnnulla: () => void
}

/** Dialog di conferma per azioni distruttive (eliminazioni, annullamenti) — sostituisce window.confirm, mai usato in questa app. */
export function ConfirmDialog({ titolo, messaggio, testoConferma = 'Elimina', pericoloso = true, inCorso = false, onConferma, onAnnulla }: Props) {
  return (
    <Dialog open onClose={onAnnulla} maxWidth="xs" fullWidth>
      <DialogTitle>{titolo}</DialogTitle>
      <DialogContent>
        <DialogContentText>{messaggio}</DialogContentText>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onAnnulla} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color={pericoloso ? 'error' : 'secondary'} onClick={onConferma} disabled={inCorso}>
          {testoConferma}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
