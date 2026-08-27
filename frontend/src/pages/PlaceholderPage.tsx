import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { tokens } from '../theme'

export function PlaceholderPage({ titolo }: { titolo: string }) {
  return (
    <Box
      sx={{
        border: `1px dashed ${tokens.surfaceBorder}`,
        borderRadius: 2,
        p: 6,
        textAlign: 'center',
        color: tokens.textSecondary,
      }}
    >
      <Typography sx={{ fontWeight: 700, color: tokens.textPrimary, mb: 0.5 }}>{titolo}</Typography>
      <Typography sx={{ fontSize: 14 }}>Questo modulo non è ancora stato implementato nel nuovo frontend.</Typography>
    </Box>
  )
}
