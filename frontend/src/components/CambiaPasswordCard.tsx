import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { useCambiaPasswordPropria } from '../api/utenti'
import { useMobile } from '../lib/useMobile'
import { useToast } from '../toast/ToastContext'
import { fontDisplay, tokens } from '../theme'

/**
 * Cambio della propria password. Richiede sempre anche quella attuale, per chiunque — Super Admin
 * compreso: una sessione lasciata aperta su un computer non deve bastare a cambiare la password e
 * chiudere fuori il legittimo proprietario. È diverso dal reset di supporto del Super Admin, che
 * infatti la password attuale non la chiede perché serve proprio a chi non la ricorda più.
 */
export function CambiaPasswordCard() {
  const mobile = useMobile()
  const [passwordAttuale, setPasswordAttuale] = useState('')
  const [passwordNuova, setPasswordNuova] = useState('')
  const toast = useToast()

  const cambiaPassword = useCambiaPasswordPropria()

  function salva() {
    if (passwordAttuale.trim() === '' || passwordNuova.trim() === '') {
      toast.errore('Compila entrambi i campi.')
      return
    }
    cambiaPassword.mutate(
      { passwordAttuale, passwordNuova },
      {
        onSuccess: () => {
          toast.successo('Password aggiornata.')
          setPasswordAttuale('')
          setPasswordNuova('')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 560 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Cambia la tua password</Typography>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Password attuale" type="password" value={passwordAttuale} onChange={(e) => setPasswordAttuale(e.target.value)} fullWidth disabled={cambiaPassword.isPending} />
        <TextField label="Nuova password" type="password" value={passwordNuova} onChange={(e) => setPasswordNuova(e.target.value)} fullWidth disabled={cambiaPassword.isPending} />
      </Box>
      <Box>
        <Button variant="contained" color="primary" onClick={salva} disabled={cambiaPassword.isPending}>
          Aggiorna password
        </Button>
      </Box>
    </Box>
  )
}
