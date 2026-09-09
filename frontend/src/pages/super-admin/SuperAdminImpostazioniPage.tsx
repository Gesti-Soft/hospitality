import { useEffect, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useAggiornaImpostazioniGlobali, useImpostazioniGlobali } from '../../api/superAdminImpostazioni'
import { ApiError } from '../../api/client'
import { fontDisplay, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'

export function SuperAdminImpostazioniPage() {
  const { isSuperAdmin } = useStruttura()
  const impostazioni = useImpostazioniGlobali(isSuperAdmin)
  const aggiorna = useAggiornaImpostazioniGlobali()
  const toast = useToast()
  const [idSoftwarePaytourist, setIdSoftwarePaytourist] = useState('')
  const [tokenWubook, setTokenWubook] = useState('')

  useEffect(() => {
    if (impostazioni.data) {
      setIdSoftwarePaytourist(impostazioni.data.idSoftwarePaytourist != null ? String(impostazioni.data.idSoftwarePaytourist) : '')
      setTokenWubook(impostazioni.data.tokenWubook ?? '')
    }
  }, [impostazioni.data])

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  function salva() {
    aggiorna.mutate(
      { idSoftwarePaytourist: idSoftwarePaytourist.trim() === '' ? null : Math.trunc(Number(idSoftwarePaytourist)), tokenWubook: tokenWubook.trim() || null },
      {
        onSuccess: () => toast.successo('Impostazioni globali salvate.'),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 720 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Globali</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Valori unici per tutta l'applicazione, non per Cliente/Struttura. Il Codice struttura Wubook e la scadenza della licenza restano
          per Struttura — si assegnano dalla Dashboard Super Admin, sul pulsante "Strutture" di ogni Cliente.
        </Typography>

        {impostazioni.isLoading && <Skeleton variant="rounded" height={140} />}
        {!impostazioni.isLoading && (
          <>
            <TextField
              label="Token Wubook"
              value={tokenWubook}
              onChange={(e) => setTokenWubook(e.target.value)}
              disabled={aggiorna.isPending}
              fullWidth
              helperText="Uguale per tutte le Strutture — l'unico account partner Wubook di GestiSoft, non un dato per Struttura."
            />
            <TextField
              label="Id Software PayTourist"
              value={idSoftwarePaytourist}
              onChange={(e) => setIdSoftwarePaytourist(e.target.value)}
              disabled={aggiorna.isPending}
              fullWidth
              helperText="Richiesto dall'API PayTourist per identificare GestiSoft come software integrato."
            />
            <Box>
              <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
                Salva
              </Button>
            </Box>
          </>
        )}
      </Box>
    </Box>
  )
}
