import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useAuth } from '../auth/AuthContext'
import { fontDisplay, tokens } from '../theme'
import { GestiSoftMark } from '../components/GestiSoftMark'
import { IconEsci } from '../layout/navIcons'

/**
 * Schermata mostrata al posto del gestionale quando un Cliente (non Super Admin) ha effettuato
 * l'accesso ma non ha ancora nessuna Struttura: solo il Super Admin può crearne una (richiesta
 * esplicita, non più un'autocreazione guidata come in precedenza), quindi qui non c'è altro da
 * fare che avvisare e rimandare al contatto con GestiSoft.
 */
export function OnboardingStrutturaPage() {
  const { esci } = useAuth()

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: tokens.paper, display: 'flex', flexDirection: 'column' }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          px: 4,
          py: 2.25,
          borderBottom: `1px solid ${tokens.surfaceBorder}`,
          bgcolor: tokens.surface,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
          <GestiSoftMark size={28} />
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>GestiSoft</Typography>
        </Box>
        <Tooltip title="Esci">
          <IconButton size="small" onClick={esci}>
            <IconEsci />
          </IconButton>
        </Tooltip>
      </Box>

      <Box sx={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 4.5 }}>
        <Box sx={{ width: '100%', maxWidth: 480, textAlign: 'center' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 22 }}>Nessuna struttura configurata</Typography>
          <Typography sx={{ fontSize: 14, color: tokens.textSecondary, mt: 1.5 }}>
            Il tuo account non ha ancora nessuna struttura attiva. L'attivazione di una nuova struttura è a cura di GestiSoft — contatta
            l'assistenza per procedere.
          </Typography>
        </Box>
      </Box>
    </Box>
  )
}
