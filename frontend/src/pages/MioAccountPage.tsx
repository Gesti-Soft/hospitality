import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { CambiaPasswordCard } from '../components/CambiaPasswordCard'
import { DueFattoriCard } from '../components/DueFattoriCard'
import { useAuth } from '../auth/AuthContext'
import { tokens } from '../theme'

/**
 * Sicurezza del proprio accesso, raggiungibile da chiunque sia autenticato: nessun permesso
 * richiesto e nessuna struttura da selezionare prima. È deliberato — il riquadro del 2FA viveva
 * solo dentro Amministrazione > Utenti, e lì non ci arrivano né un Super Admin che non ha ancora
 * scelto una struttura né un dipendente senza il permesso di gestione utenti: cioè quasi tutti
 * quelli a cui il promemoria di attivarlo viene mostrato.
 */
export function MioAccountPage() {
  const { sessione } = useAuth()

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>
        Stai modificando l'accesso di <b>{sessione?.email}</b>.
      </Typography>

      <CambiaPasswordCard />

      <DueFattoriCard />
    </Box>
  )
}
