import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import CheckCircleIcon from '@mui/icons-material/CheckCircleOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { StatoCamera, useCamere, useSegnaCameraPulita } from '../api/camere'
import { ApiError } from '../api/client'
import { fontDisplay, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'

export function PuliziePage() {
  const { strutturaId } = useStruttura()
  const camere = useCamere(strutturaId)
  const segnaPulita = useSegnaCameraPulita(strutturaId)
  const toast = useToast()

  const daPulire = (camere.data ?? []).filter((c) => c.stateRoom === StatoCamera.DaPulire)

  function segna(cameraId: string, nome: string) {
    segnaPulita.mutate(cameraId, {
      onSuccess: () => toast.successo(`${nome} segnata come pulita.`),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>

      {camere.isLoading && <Skeleton variant="rounded" height={220} />}

      {!camere.isLoading && daPulire.length === 0 && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 4, textAlign: 'center' }}>
          <Typography sx={{ fontSize: 14, color: tokens.textSecondary }}>Nessuna camera da pulire al momento.</Typography>
        </Box>
      )}

      {!camere.isLoading && daPulire.length > 0 && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {daPulire.map((c) => (
            <Box
              key={c.id}
              sx={{
                border: `1.5px solid ${tokens.wait600}`,
                borderRadius: 2,
                bgcolor: tokens.surface,
                p: 2.5,
                display: 'flex',
                flexDirection: 'column',
                gap: 1.5,
              }}
            >
              <Box>
                <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 17 }}>{c.nome}</Typography>
                {c.tipologiaNome && <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mt: 0.25 }}>{c.tipologiaNome}</Typography>}
              </Box>
              <Button
                variant="contained"
                color="primary"
                size="large"
                startIcon={<CheckCircleIcon />}
                onClick={() => segna(c.id, c.nome)}
                disabled={segnaPulita.isPending}
                sx={{ mt: 'auto' }}
              >
                Segna pulita
              </Button>
            </Box>
          ))}
        </Box>
      )}
    </Box>
  )
}
