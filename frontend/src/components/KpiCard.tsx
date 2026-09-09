import type { ReactNode } from 'react'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { fontMono, tokens } from '../theme'

type ColoreAccento = 'orange' | 'error'

const BORDO_ACCENTO: Record<ColoreAccento, string> = { orange: tokens.orange600, error: tokens.error600 }
const TESTO_ACCENTO: Record<ColoreAccento, string> = { orange: tokens.orange700, error: tokens.error600 }

/**
 * Card KPI riusata da Cruscotto, Dashboard Super Admin e Statistiche (struttura/Super Admin) —
 * centralizzata qui per non duplicarla una terza volta. `valore: null` mostra uno Skeleton al posto
 * del numero (caricamento in corso); passare una stringa già pronta se la pagina non ha bisogno di
 * un caricamento per-KPI. `icona` è opzionale: un'icona MUI in un quadratino azzurro sopra l'etichetta.
 */
export function KpiCard({
  etichetta,
  valore,
  dettaglio,
  accento,
  coloreAccento = 'orange',
  icona,
}: {
  etichetta: string
  valore: string | null
  dettaglio?: string
  accento?: boolean
  coloreAccento?: ColoreAccento
  icona?: ReactNode
}) {
  return (
    <Box
      sx={{
        bgcolor: tokens.surface,
        border: `1px solid ${accento ? BORDO_ACCENTO[coloreAccento] : tokens.surfaceBorder}`,
        borderWidth: accento ? 1.5 : 1,
        borderRadius: 2,
        p: '20px 22px',
      }}
    >
      {!icona && (
        <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: accento ? TESTO_ACCENTO[coloreAccento] : tokens.textSecondary }}>{etichetta}</Typography>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mt: icona ? 0 : 1 }}>
        {icona && (
          <Box
            sx={{
              width: 38,
              height: 38,
              flexShrink: 0,
              borderRadius: 1.5,
              bgcolor: tokens.blue100,
              color: tokens.blue600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              '& svg': { fontSize: 20 },
            }}
          >
            {icona}
          </Box>
        )}
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
          {valore === null ? (
            <Skeleton width={60} height={34} />
          ) : (
            <Typography sx={{ fontFamily: fontMono, fontSize: 28, fontWeight: 600 }}>{valore}</Typography>
          )}
          {dettaglio && valore !== null && <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>{dettaglio}</Typography>}
        </Box>
      </Box>
      {icona && (
        <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: accento ? TESTO_ACCENTO[coloreAccento] : tokens.textSecondary, mt: 0.75 }}>
          {etichetta}
        </Typography>
      )}
    </Box>
  )
}

export function KpiCardDoppia({ etichetta, voci }: { etichetta: string; voci: { valore: string | null; etichetta: string }[] }) {
  return (
    <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: '20px 22px' }}>
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: tokens.textSecondary }}>{etichetta}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 3, mt: 1 }}>
        {voci.map((v) => (
          <Box key={v.etichetta} sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
            {v.valore === null ? (
              <Skeleton width={40} height={34} />
            ) : (
              <Typography sx={{ fontFamily: fontMono, fontSize: 28, fontWeight: 600 }}>{v.valore}</Typography>
            )}
            {v.valore !== null && <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>{v.etichetta}</Typography>}
          </Box>
        ))}
      </Box>
    </Box>
  )
}
