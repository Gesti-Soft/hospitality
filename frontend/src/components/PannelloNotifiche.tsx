import { useEffect, useRef, useState } from 'react'
import Badge from '@mui/material/Badge'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Popover from '@mui/material/Popover'
import Typography from '@mui/material/Typography'
import { tokens } from '../theme'
import { IconNotifiche } from '../layout/navIcons'
import { useContoNotificheNonLette, useNotifiche, useSegnaNotificaLetta, useSegnaTutteNotificheLette, type NotificaDto } from '../api/notifiche'

// Notifiche mostrate alla volta: si parte con una sola pagina, poi se ne rivela un'altra ogni volta
// che si scorre fino in fondo all'elenco (stesso principio di usePaginazioneScroll usato altrove,
// qui reimplementato localmente perché quell'hook è tipizzato per righe di tabella, non per un
// elenco in un pannello a tendina) — i dati sono già tutti caricati in un solo giro (fino a 100, vedi
// NotificaRepository), qui si rivelano solo progressivamente, nessuna nuova chiamata di rete.
const NOTIFICHE_PER_PAGINA = 5

/** Bottone campanella con badge non-lette + pannello a tendina, nella topbar (vedi AppShell). */
export function PannelloNotifiche({ strutturaId }: { strutturaId: string | null }) {
  const [ancora, setAncora] = useState<HTMLElement | null>(null)
  const [numeroVisibili, setNumeroVisibili] = useState(NOTIFICHE_PER_PAGINA)
  const { data: conteggio } = useContoNotificheNonLette(strutturaId)
  const { data: notifiche } = useNotifiche(strutturaId, false)
  const segnaLetta = useSegnaNotificaLetta(strutturaId)
  const segnaTutteLette = useSegnaTutteNotificheLette(strutturaId)

  const contenitoreRef = useRef<HTMLDivElement | null>(null)
  const sentinellaRef = useRef<HTMLDivElement | null>(null)
  const totale = notifiche?.length ?? 0
  const altreDaCaricare = numeroVisibili < totale

  useEffect(() => {
    if (!altreDaCaricare) return
    const el = sentinellaRef.current
    if (!el) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          setNumeroVisibili((n) => n + NOTIFICHE_PER_PAGINA)
        }
      },
      { root: contenitoreRef.current, rootMargin: '100px' },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [altreDaCaricare])

  if (!strutturaId) {
    return null
  }

  const aperto = !!ancora

  function apri(elemento: HTMLElement) {
    setAncora(elemento)
    // Riparte sempre dalle prime 5 alla riapertura, non da dove si era arrivati l'ultima volta.
    setNumeroVisibili(NOTIFICHE_PER_PAGINA)
  }

  const notificheVisibili = (notifiche ?? []).slice(0, numeroVisibili)

  return (
    <>
      <IconButton size="small" onClick={(e) => apri(e.currentTarget)} sx={{ color: '#7E899A' }}>
        <Badge badgeContent={conteggio ?? 0} color="error" max={99} sx={{ '& .MuiBadge-badge': { fontSize: 9.5, fontWeight: 700 } }}>
          <IconNotifiche width={19} height={19} />
        </Badge>
      </IconButton>

      <Popover
        open={aperto}
        anchorEl={ancora}
        onClose={() => setAncora(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 380, maxHeight: 480, bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}` } } }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 2, py: 1.5, borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
          <Typography sx={{ fontWeight: 800, fontSize: 14 }}>Notifiche</Typography>
          {!!conteggio && conteggio > 0 && (
            <Button size="small" onClick={() => segnaTutteLette.mutate()} disabled={segnaTutteLette.isPending}>
              Segna tutte come lette
            </Button>
          )}
        </Box>

        <Box ref={contenitoreRef} sx={{ maxHeight: 400, overflowY: 'auto' }}>
          {!notifiche || notifiche.length === 0 ? (
            <Typography sx={{ p: 2.5, fontSize: 13, color: tokens.textSecondary, textAlign: 'center' }}>Nessuna notifica.</Typography>
          ) : (
            <>
              {notificheVisibili.map((n) => (
                <RigaNotifica key={n.id} notifica={n} onClick={() => segnaLetta.mutate(n.id)} />
              ))}
              {altreDaCaricare && <Box ref={sentinellaRef} sx={{ height: 1 }} />}
            </>
          )}
        </Box>
      </Popover>
    </>
  )
}

function RigaNotifica({ notifica, onClick }: { notifica: NotificaDto; onClick: () => void }) {
  const nonLetta = notifica.lettaAtUtc === null
  const data = new Date(notifica.createdAtUtc)

  return (
    <Box
      component="button"
      onClick={onClick}
      sx={{
        display: 'flex',
        gap: 1,
        width: '100%',
        textAlign: 'left',
        px: 2,
        py: 1.25,
        border: 'none',
        borderBottom: `1px solid ${tokens.surfaceBorder}`,
        bgcolor: nonLetta ? 'rgba(28,126,168,0.06)' : 'transparent',
        cursor: 'pointer',
        '&:hover': { bgcolor: 'rgba(0,0,0,0.03)' },
      }}
    >
      <Box sx={{ flex: '0 0 auto', pt: 0.5 }}>
        <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: nonLetta ? tokens.blue600 : 'transparent' }} />
      </Box>
      <Box sx={{ minWidth: 0, flex: 1 }}>
        <Typography sx={{ fontSize: 13, fontWeight: nonLetta ? 700 : 500 }}>{notifica.titolo}</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mt: 0.25 }}>{notifica.messaggio}</Typography>
        <Typography sx={{ fontSize: 11, color: tokens.textTertiary, mt: 0.5 }}>
          {data.toLocaleString('it-IT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}
        </Typography>
      </Box>
    </Box>
  )
}
