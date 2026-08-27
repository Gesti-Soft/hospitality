import { useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { useArriviInCorso, useArriviProssimi, useStoricoPrenotazioni, type PrenotazioneDto } from '../api/prenotazioni'
import { useOspite } from '../api/ospiti'
import { fontMono, tokens } from '../theme'
import { OspiteDialog } from '../components/OspiteDialog'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

type VistaOspiti = 'arrivi' | 'in-corso' | 'storico'

export function OspitiPage() {
  const { strutturaId } = useStruttura()
  const [vista, setVista] = useState<VistaOspiti>('arrivi')
  const [prenotazioneAperta, setPrenotazioneAperta] = useState<PrenotazioneDto | null>(null)

  const arrivi = useArriviProssimi(strutturaId)
  const inCorso = useArriviInCorso(strutturaId)
  const storico = useStoricoPrenotazioni(strutturaId, new Date().getFullYear())

  const { dati, caricamento } =
    vista === 'arrivi'
      ? { dati: arrivi.data, caricamento: arrivi.isLoading }
      : vista === 'in-corso'
        ? { dati: inCorso.data, caricamento: inCorso.isLoading }
        : { dati: storico.data, caricamento: storico.isLoading }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Tabs value={vista} onChange={(_, v) => setVista(v)} sx={{ minHeight: 0 }}>
        <Tab label="Arrivi" value="arrivi" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="In corso" value="in-corso" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Storico" value="storico" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
      </Tabs>

      {!caricamento && (dati ?? []).length > 0 && (
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Clicca su una riga per compilare o consultare la scheda alloggiati e calcolare la tassa di soggiorno.
        </Typography>
      )}

      {caricamento && <Skeleton variant="rounded" height={260} />}

      {!caricamento && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Prenotazione</TableCell>
                <TableCell>Camera</TableCell>
                <TableCell>Check-in</TableCell>
                <TableCell>Check-out</TableCell>
                <TableCell align="right">Ospiti</TableCell>
                <TableCell align="right">Tassa soggiorno</TableCell>
                <TableCell>Scheda alloggiati</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(dati ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna prenotazione in questa vista.
                  </TableCell>
                </TableRow>
              )}
              {(dati ?? []).map((p) => (
                <TableRow key={p.id} hover onClick={() => setPrenotazioneAperta(p)} sx={{ cursor: 'pointer' }}>
                  <TableCell sx={{ fontWeight: 700 }}>{p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.cameraNome ?? '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.checkIn ? formattatoreData.format(new Date(p.checkIn)) : '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.checkOut ? formattatoreData.format(new Date(p.checkOut)) : '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {p.numeroOspiti ?? '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {p.totalTax != null ? formattatoreValuta.format(p.totalTax) : '—'}
                  </TableCell>
                  <TableCell>
                    <StatoSchedaChip strutturaId={strutturaId} prenotazioneId={p.id} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {prenotazioneAperta && strutturaId && (
        <OspiteDialog strutturaId={strutturaId} prenotazione={prenotazioneAperta} onClose={() => setPrenotazioneAperta(null)} />
      )}
    </Box>
  )
}

// La scheda ospiti è un'entità separata dalla Prenotazione (vedi api/ospiti.ts): `numeroOspiti`
// sulla prenotazione può essere impostato direttamente alla creazione, senza che esista ancora
// una scheda — va verificato per-riga con una vera GET, non dedotto da quel campo.
function StatoSchedaChip({ strutturaId, prenotazioneId }: { strutturaId: string | null; prenotazioneId: string }) {
  const ospite = useOspite(strutturaId, prenotazioneId)

  if (ospite.isLoading) {
    return <Chip size="small" label="…" sx={{ bgcolor: tokens.surfaceBorder, color: tokens.textSecondary, fontWeight: 700 }} />
  }

  return ospite.data ? (
    <Chip size="small" label="Compilata" sx={{ bgcolor: tokens.ok600, color: '#fff', fontWeight: 700 }} />
  ) : (
    <Chip size="small" label="Da compilare" sx={{ bgcolor: tokens.wait600, color: '#fff', fontWeight: 700 }} />
  )
}
