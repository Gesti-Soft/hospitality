import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { scaricaBackupDatabase } from '../api/superAdmin'
import { ApiError } from '../api/client'
import { LivelloLog, useLogs } from '../api/log'
import { fontDisplay, fontMono, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

const ETICHETTA_LIVELLO: Record<LivelloLog, string> = {
  [LivelloLog.Info]: 'OK',
  [LivelloLog.Warning]: 'Avviso',
  [LivelloLog.Error]: 'Errore',
}

const COLORE_LIVELLO: Record<LivelloLog, string> = {
  [LivelloLog.Info]: tokens.blue600,
  [LivelloLog.Warning]: tokens.wait600,
  [LivelloLog.Error]: tokens.error600,
}

export function SuperAdminBackupPage() {
  const { isSuperAdmin } = useStruttura()
  const toast = useToast()
  const [inCorso, setInCorso] = useState(false)
  const storico = useLogs(null, null, 'Backup', '', '', '', 10)
  const eventi = storico.data?.pages.flatMap((p) => p.items) ?? []

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  async function scarica() {
    setInCorso(true)
    try {
      await scaricaBackupDatabase()
      toast.successo('Backup scaricato.')
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    } finally {
      setInCorso(false)
    }
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 18 }}>Backup</Typography>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 720 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Scarica un backup adesso</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Genera al momento un dump completo del database e lo scarica nel browser — indipendente
          dai backup automatici notturni già attivi sul server (dump fisico + WAL in continuo,
          retention 30 giorni, più un dump logico separato). Utile per avere una copia subito, in
          qualunque momento, senza accedere al server.
        </Typography>

        <Box>
          <Button variant="contained" color="primary" onClick={scarica} disabled={inCorso}>
            {inCorso ? 'Generazione in corso…' : 'Scarica backup adesso'}
          </Button>
        </Box>
      </Box>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1, maxWidth: 720 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Backup automatici sul server</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Backup fisico completo + dump logico: ogni notte alle 3:00 · Controllo archiviazione WAL: ogni ora ·
          Test di restore (verifica che i backup siano davvero ripristinabili): ogni 4 settimane. Retention 30 giorni
          (backup fisico + WAL), 7 giorni (dump logico). Dettagli completi in docs/backup-restore.md.
        </Typography>
      </Box>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5, maxWidth: 720 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Ultimi backup eseguiti</Typography>

        {storico.isLoading && <Skeleton variant="rounded" height={100} />}

        {!storico.isLoading && eventi.length === 0 && (
          <Typography sx={{ fontSize: 13, color: tokens.textTertiary }}>Nessun backup automatico ancora registrato.</Typography>
        )}

        {eventi.map((e) => (
          <Box key={e.id} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5, py: 1, borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
            <Chip size="small" label={ETICHETTA_LIVELLO[e.livello]} sx={{ bgcolor: COLORE_LIVELLO[e.livello], color: '#fff', fontWeight: 700, flexShrink: 0 }} />
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25 }}>
              <Typography sx={{ fontSize: 13 }}>{e.messaggio}</Typography>
              <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary, fontFamily: fontMono }}>{formattatoreDataOra.format(new Date(e.createdAtUtc))}</Typography>
            </Box>
          </Box>
        ))}
      </Box>
    </Box>
  )
}
