import { useState } from 'react'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAnniDisponibiliFinanze, useRiepilogoCassa } from '../api/finanze'
import { fontMono, tokens } from '../theme'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../lib/anni'
import { formattatoreValuta, IntestazioneFinanze } from '../components/finanze/FinanzeComuni'

export function RiepilogoCassaPage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniDisponibiliFinanze(strutturaId)
  const anni = anniConAnnoCorrente(anniDisponibili.data)
  const riepilogo = useRiepilogoCassa(strutturaId, anno)
  const dati = riepilogo.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze titolo="Riepilogo" anno={anno} anni={anni} onAnnoChange={setAnno} />

      {riepilogo.isLoading && <Skeleton variant="rounded" height={160} />}

      {dati && (
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
          {[
            { etichetta: `Incassi prenotazioni (${anno})`, valore: dati.importoPagatoPrenotazioni, segno: '+' as const },
            { etichetta: `Cauzioni trattenute (${anno})`, valore: dati.cauzioni, segno: '+' as const },
            { etichetta: `Entrate (${anno})`, valore: dati.entrate, segno: '+' as const },
            { etichetta: `Spese (${anno})`, valore: dati.spese, segno: '-' as const },
          ].map((r) => {
            // Incassi/Cauzioni/Entrate sono sempre un'entrata, Spese sempre un'uscita — il colore
            // segue il segno concettuale della voce, non quello del numero grezzo (le Spese sono
            // memorizzate come importo positivo, il "−" è solo visivo).
            const colore = r.segno === '-' ? tokens.error600 : tokens.ok600
            return (
              <Box key={r.etichetta} sx={{ border: `1.5px solid ${colore}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.textSecondary }}>{r.etichetta}</Typography>
                <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 600, mt: 0.75, color: colore }}>
                  {r.segno === '-' ? '−' : ''}
                  {formattatoreValuta.format(r.valore)}
                </Typography>
              </Box>
            )
          })}
          {[
            { etichetta: `Saldo netto (${anno})`, valore: dati.saldo },
            { etichetta: 'Cassa attuale', valore: dati.cassaAttuale },
          ].map((r) => {
            const colore = r.valore < 0 ? tokens.error600 : tokens.ok600
            return (
              <Box key={r.etichetta} sx={{ border: `1.5px solid ${colore}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.25 }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.textSecondary }}>{r.etichetta}</Typography>
                <Typography sx={{ fontFamily: fontMono, fontSize: 22, fontWeight: 700, mt: 0.75, color: colore }}>{formattatoreValuta.format(r.valore)}</Typography>
              </Box>
            )
          })}
        </Box>
      )}
    </Box>
  )
}
