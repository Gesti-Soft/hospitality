import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { useAuth } from '../../auth/AuthContext'
import { useDashboardSuperAdmin } from '../../api/superAdmin'
import { LogPage } from '../amministrazione/LogPage'
import { fontDisplay, tokens } from '../../theme'

/**
 * Log di sicurezza su tutti i Clienti: accessi (riusciti, falliti, account bloccati, codici di
 * verifica sbagliati) e azioni dello staff GestiSoft. È la stessa pagina del log di struttura, ma
 * senza filtro per struttura — cosa che il backend concede solo al Super Admin — e con le sole
 * categorie che riguardano la sicurezza, invece dell'elenco completo dove finirebbero annegate.
 */
export function SuperAdminLogPage() {
  const { sessione } = useAuth()
  const dashboard = useDashboardSuperAdmin(!!sessione?.isSuperAdmin)

  if (!sessione?.isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  // Gli eventi di sicurezza portano il Cliente ma quasi mai la struttura (un login non appartiene a
  // nessuna struttura in particolare): qui si mostra quindi il Cliente, risolto dall'elenco già
  // caricato per il cruscotto invece che con una chiamata in più.
  const nomiClienti = new Map((dashboard.data?.clienti ?? []).map((c) => [c.id, c.ragioneSociale]))
  // Mai "GestiSoft" per gli eventi senza Cliente: esiste un Cliente che si chiama proprio così,
  // e le due cose diventerebbero indistinguibili in elenco.
  const nomeCliente = (clienteId: string | null) => (clienteId ? (nomiClienti.get(clienteId) ?? 'Cliente eliminato') : '—')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Accessi e sicurezza</Typography>
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary, mt: 0.5 }}>
          Tutti i Clienti insieme: accessi riusciti e falliti, account bloccati per troppi tentativi, codici di verifica
          sbagliati e ogni azione compiuta dallo staff GestiSoft. Le righe senza Cliente ("—") sono eventi di
          sistema o azioni fatte da qui.
        </Typography>
      </Box>

      <LogPage strutturaId={null} categorie={['Auth', 'SuperAdmin', 'Utente', 'Backup']} nomeCliente={nomeCliente} />
    </Box>
  )
}
