import { forwardRef, type ReactNode } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import { fontDisplay, tokens } from '../theme'
import { useMobile } from '../lib/useMobile'

interface CardElencoProps {
  children: ReactNode
  /** Bordo colorato per riflettere uno stato (es. stato camera/prenotazione), come già in PuliziePage. Default: bordo neutro. */
  coloreAccento?: string
  /** Rende l'intera card cliccabile (es. per aprire il dettaglio), come il click sulla riga in OspitiPage. */
  onClick?: () => void
}

/** Contenitore "riga" in versione card per gli elenchi su mobile, alternativo alle righe di `<Table>` usate su desktop. Un `Box` per pagina, stesso look di `PuliziePage`. */
export function CardElenco({ children, coloreAccento, onClick }: CardElencoProps) {
  return (
    <Box
      onClick={onClick}
      sx={{
        border: `1.5px solid ${coloreAccento ?? tokens.surfaceBorder}`,
        borderRadius: 2,
        bgcolor: tokens.surface,
        p: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.25,
        cursor: onClick ? 'pointer' : undefined,
      }}
    >
      {children}
    </Box>
  )
}

/** Riga di coppie etichetta/valore che va a capo, per rendere in poco spazio le colonne "extra" di una tabella dentro una CardElenco. */
export function RigaCardMeta({ voci }: { voci: { etichetta: string; valore: ReactNode }[] }) {
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.75, rowGap: 1 }}>
      {voci.map((v, i) => (
        <Box key={i} sx={{ minWidth: 88 }}>
          <Typography sx={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '.05em', color: tokens.textTertiary, textTransform: 'uppercase' }}>
            {v.etichetta}
          </Typography>
          {/* component="div" (non il "p" di default): `valore` a volte è un Chip/altro elemento a
              blocco (es. StatoSchedaChip in OspitiPage), non valido dentro un <p>. */}
          <Typography component="div" sx={{ fontSize: 13, fontWeight: 600, color: tokens.textPrimary, mt: 0.25 }}>{v.valore}</Typography>
        </Box>
      ))}
    </Box>
  )
}

/** Riga superiore di una CardElenco: titolo (es. nome ospite/camera) + eventuale badge di stato a destra. */
export function TestataCardElenco({ titolo, sottotitolo, azioneDestra }: { titolo: ReactNode; sottotitolo?: ReactNode; azioneDestra?: ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 1 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>{titolo}</Typography>
        {sottotitolo && (
          <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mt: 0.25 }}>{sottotitolo}</Typography>
        )}
      </Box>
      {azioneDestra && <Box sx={{ flex: '0 0 auto' }}>{azioneDestra}</Box>}
    </Box>
  )
}

/** Riga di azioni (icone/bottoni) in fondo a una CardElenco, separata dal contenuto sopra. */
export function AzioniCardElenco({ children }: { children: ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.5, pt: 0.25, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
      {children}
    </Box>
  )
}

/**
 * Pulsante primario "+ Nuovo/a ..." usato nelle intestazioni di pagina/tab: su mobile mostra solo
 * l'icona (etichetta completa nel tooltip) per non occupare spazio prezioso nell'intestazione,
 * su desktop il testo per esteso come sempre. `icona` di default è un "+"; `variant="outlined"`
 * per le azioni secondarie (es. "Assegna utente esistente").
 */
export function BottoneNuovo({
  etichetta,
  onClick,
  disabilitato,
  size = 'small',
  variant = 'contained',
  icona,
}: {
  etichetta: string
  onClick: () => void
  disabilitato?: boolean
  size?: 'small' | 'medium'
  variant?: 'contained' | 'outlined'
  icona?: ReactNode
}) {
  const mobile = useMobile()
  if (mobile) {
    return (
      <Tooltip title={etichetta.replace(/^\+\s*/, '')}>
        <span>
          <IconButton
            color="primary"
            size={size}
            onClick={onClick}
            disabled={disabilitato}
            sx={
              variant === 'contained'
                ? {
                    bgcolor: 'primary.main',
                    color: 'primary.contrastText',
                    '&:hover': { bgcolor: 'primary.dark' },
                    '&.Mui-disabled': { bgcolor: 'action.disabledBackground', color: 'action.disabled' },
                  }
                : { border: '1.5px solid', borderColor: 'primary.main' }
            }
          >
            {icona ?? <AddIcon fontSize={size === 'medium' ? 'medium' : 'small'} />}
          </IconButton>
        </span>
      </Tooltip>
    )
  }
  return (
    <Button variant={variant} color="primary" size={size} onClick={onClick} disabled={disabilitato}>
      {etichetta}
    </Button>
  )
}

/** Equivalente di RigaVuota (FinanzeComuni) per un elenco a card: nessuna TableRow, solo un Box centrato. */
export function MessaggioVuotoElenco({ messaggio }: { messaggio: string }) {
  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, textAlign: 'center', color: tokens.textSecondary, py: 4, fontSize: 14 }}>
      {messaggio}
    </Box>
  )
}

/** Equivalente di RigaCaricamentoAltri (FinanzeComuni) per un elenco a card, come sentinella per la paginazione a scroll infinito. */
export const SentinellaCaricamentoElenco = forwardRef<HTMLDivElement>(function SentinellaCaricamentoElenco(_, ref) {
  return (
    <Box ref={ref} sx={{ textAlign: 'center', color: tokens.textTertiary, py: 2, fontSize: 12 }}>
      Caricamento altri...
    </Box>
  )
})
