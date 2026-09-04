import { useState } from 'react'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { useRiepilogoCassa } from '../api/finanze'
import { fontMono, tokens } from '../theme'
import { ANNO_CORRENTE, formattatoreValuta, IntestazioneFinanze } from '../components/finanze/FinanzeComuni'

export function RiepilogoCassaPage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const riepilogo = useRiepilogoCassa(strutturaId, anno)
  const dati = riepilogo.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze titolo="Riepilogo" anno={anno} onAnnoChange={setAnno} />

      {riepilogo.isLoading && <Skeleton variant="rounded" height={160} />}

      {dati && (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 2 }}>
          {[
            { etichetta: 'Incassi prenotazioni', valore: dati.importoPagatoPrenotazioni, segno: '+' as const },
            { etichetta: 'Cauzioni trattenute', valore: dati.cauzioni, segno: '+' as const },
            { etichetta: 'Entrate', valore: dati.entrate, segno: '+' as const },
            { etichetta: 'Spese', valore: dati.spese, segno: '-' as const },
          ].map((r) => (
            <Box key={r.etichetta} sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
              <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.textSecondary }}>{r.etichetta}</Typography>
              <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 600, mt: 0.75 }}>
                {r.segno === '-' ? '−' : ''}
                {formattatoreValuta.format(r.valore)}
              </Typography>
            </Box>
          ))}
          <Box sx={{ border: `1.5px solid ${tokens.orange600}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.orange700 }}>Saldo cassa</Typography>
            <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 700, mt: 0.75 }}>{formattatoreValuta.format(dati.saldo)}</Typography>
          </Box>
        </Box>
      )}
    </Box>
  )
}
