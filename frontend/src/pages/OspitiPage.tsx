import { useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { useStruttura } from '../struttura/StrutturaContext'
import { useArriviInCorso, useArriviProssimi, useStoricoPrenotazioni, type PrenotazioneDto } from '../api/prenotazioni'
import { useOspite } from '../api/ospiti'
import { fontMono, tokens } from '../theme'
import { OspiteDialog } from '../components/OspiteDialog'
import { inizioGiornoLocale } from '../lib/date'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreMese = new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric' })

type VistaOspiti = 'arrivi' | 'in-corso' | 'storico'

export function OspitiPage() {
  const { strutturaId } = useStruttura()
  const [vista, setVista] = useState<VistaOspiti>('arrivi')
  const [formato, setFormato] = useState<'lista' | 'calendario'>('lista')
  const [giornoSelezionato, setGiornoSelezionato] = useState<Date | null>(null)
  const [meseVisibile, setMeseVisibile] = useState(() => inizioGiornoLocale(new Date()))
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

  // Per "Arrivi" la data rilevante è il check-in, per "In corso" il check-out (prossima partenza) —
  // "Storico" non ha una vista calendario, è un archivio, non un planning.
  const campoData: 'checkIn' | 'checkOut' = vista === 'in-corso' ? 'checkOut' : 'checkIn'
  const calendarioDisponibile = vista !== 'storico'

  const datiFiltrati =
    formato === 'calendario' && giornoSelezionato
      ? (dati ?? []).filter((p) => {
          const valore = p[campoData]
          return valore && inizioGiornoLocale(new Date(valore)).getTime() === giornoSelezionato.getTime()
        })
      : dati

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <Tabs
          value={vista}
          onChange={(_, v) => {
            setVista(v)
            setGiornoSelezionato(null)
          }}
          sx={{ minHeight: 0 }}
        >
          <Tab label="Arrivi" value="arrivi" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="In corso" value="in-corso" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Storico" value="storico" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        </Tabs>
        {calendarioDisponibile && (
          <ToggleButtonGroup
            exclusive
            size="small"
            value={formato}
            onChange={(_, v) => {
              if (v) {
                setFormato(v)
                setGiornoSelezionato(null)
              }
            }}
          >
            <ToggleButton value="lista">Lista</ToggleButton>
            <ToggleButton value="calendario">Calendario</ToggleButton>
          </ToggleButtonGroup>
        )}
      </Box>

      {!caricamento && (dati ?? []).length > 0 && formato === 'lista' && (
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Clicca su una riga per compilare o consultare la scheda alloggiati e calcolare la tassa di soggiorno.
        </Typography>
      )}

      {caricamento && <Skeleton variant="rounded" height={260} />}

      {!caricamento && formato === 'calendario' && calendarioDisponibile && (
        <VistaMeseConteggio
          dati={dati ?? []}
          campoData={campoData}
          meseVisibile={meseVisibile}
          onCambiaMese={setMeseVisibile}
          giornoSelezionato={giornoSelezionato}
          onSelezionaGiorno={setGiornoSelezionato}
        />
      )}

      {!caricamento && (formato === 'lista' || giornoSelezionato) && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          {formato === 'calendario' && giornoSelezionato && (
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: '10px 14px', borderBottom: `1px solid ${tokens.surfaceBorder}`, bgcolor: tokens.paper }}>
              <Typography sx={{ fontSize: 12.5, fontWeight: 700 }}>{formattatoreData.format(giornoSelezionato)}</Typography>
              <Typography sx={{ fontSize: 12, color: tokens.blue600, cursor: 'pointer', fontWeight: 600 }} onClick={() => setGiornoSelezionato(null)}>
                Mostra tutti
              </Typography>
            </Box>
          )}
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Prenotazione</TableCell>
                <TableCell>Ospite</TableCell>
                <TableCell>Camera</TableCell>
                <TableCell>Check-in</TableCell>
                <TableCell>Check-out</TableCell>
                <TableCell align="right">Ospiti</TableCell>
                <TableCell align="right">Tassa soggiorno</TableCell>
                <TableCell>Scheda alloggiati</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(datiFiltrati ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={8} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna prenotazione in questa vista.
                  </TableCell>
                </TableRow>
              )}
              {(datiFiltrati ?? []).map((p) => (
                <TableRow key={p.id} hover onClick={() => setPrenotazioneAperta(p)} sx={{ cursor: 'pointer' }}>
                  <TableCell sx={{ fontWeight: 700 }}>{p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : '—'}</TableCell>
                  <TableCell>{p.ospiteNome || p.ospiteCognome ? `${p.ospiteNome ?? ''} ${p.ospiteCognome ?? ''}`.trim() : '—'}</TableCell>
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

const NOMI_GIORNO_BREVI = ['D', 'L', 'M', 'M', 'G', 'V', 'S']

function VistaMeseConteggio({
  dati,
  campoData,
  meseVisibile,
  onCambiaMese,
  giornoSelezionato,
  onSelezionaGiorno,
}: {
  dati: PrenotazioneDto[]
  campoData: 'checkIn' | 'checkOut'
  meseVisibile: Date
  onCambiaMese: (d: Date) => void
  giornoSelezionato: Date | null
  onSelezionaGiorno: (d: Date | null) => void
}) {
  const conteggioPerGiorno = new Map<number, number>()
  for (const p of dati) {
    const valore = p[campoData]
    if (!valore) continue
    const d = inizioGiornoLocale(new Date(valore))
    if (d.getFullYear() !== meseVisibile.getFullYear() || d.getMonth() !== meseVisibile.getMonth()) continue
    conteggioPerGiorno.set(d.getTime(), (conteggioPerGiorno.get(d.getTime()) ?? 0) + 1)
  }

  const primoDelMese = new Date(meseVisibile.getFullYear(), meseVisibile.getMonth(), 1)
  const giorniNelMese = new Date(meseVisibile.getFullYear(), meseVisibile.getMonth() + 1, 0).getDate()
  const offsetIniziale = primoDelMese.getDay()
  const celle: (Date | null)[] = [
    ...Array.from({ length: offsetIniziale }, () => null),
    ...Array.from({ length: giorniNelMese }, (_, i) => new Date(meseVisibile.getFullYear(), meseVisibile.getMonth(), i + 1)),
  ]
  const oggi = inizioGiornoLocale(new Date())

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 2, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <IconButton size="small" onClick={() => onCambiaMese(new Date(meseVisibile.getFullYear(), meseVisibile.getMonth() - 1, 1))}>
          <ChevronLeftIcon fontSize="small" />
        </IconButton>
        <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 150, textAlign: 'center', textTransform: 'capitalize' }}>
          {formattatoreMese.format(meseVisibile)}
        </Typography>
        <IconButton size="small" onClick={() => onCambiaMese(new Date(meseVisibile.getFullYear(), meseVisibile.getMonth() + 1, 1))}>
          <ChevronRightIcon fontSize="small" />
        </IconButton>
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 0.75 }}>
        {NOMI_GIORNO_BREVI.map((n, i) => (
          <Typography key={i} sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textAlign: 'center' }}>
            {n}
          </Typography>
        ))}
        {celle.map((giorno, i) => {
          if (!giorno) return <Box key={i} />
          const conteggio = conteggioPerGiorno.get(giorno.getTime()) ?? 0
          const selezionato = giornoSelezionato?.getTime() === giorno.getTime()
          const isOggi = giorno.getTime() === oggi.getTime()
          return (
            <Box
              key={i}
              onClick={() => onSelezionaGiorno(selezionato ? null : giorno)}
              sx={{
                aspectRatio: '1',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: 1.5,
                cursor: 'pointer',
                border: `1px solid ${selezionato ? tokens.blue600 : isOggi ? tokens.blue400 : 'transparent'}`,
                bgcolor: selezionato ? tokens.blue600 : conteggio > 0 ? tokens.blue100 : 'transparent',
                '&:hover': { bgcolor: selezionato ? tokens.blue600 : tokens.paper },
              }}
            >
              <Typography sx={{ fontSize: 12, fontWeight: isOggi ? 700 : 500, color: selezionato ? '#fff' : tokens.textPrimary }}>
                {giorno.getDate()}
              </Typography>
              {conteggio > 0 && (
                <Typography sx={{ fontSize: 9.5, fontWeight: 700, color: selezionato ? '#fff' : tokens.blue600 }}>{conteggio}</Typography>
              )}
            </Box>
          )
        })}
      </Box>
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
