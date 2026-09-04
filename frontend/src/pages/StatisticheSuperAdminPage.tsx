import { useEffect, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { BarChart } from '@mui/x-charts/BarChart'
import { useStruttura } from '../struttura/StrutturaContext'
import {
  EsitoIntegrazione,
  useAnniDisponibiliStatisticheSuperAdmin,
  useStatisticheSuperAdmin,
  type EsitoIntegrazioneDto,
} from '../api/statisticheSuperAdmin'
import { KpiCard } from '../components/KpiCard'
import { NOMI_MESI } from '../components/statistiche/MatriceTipologieMese'
import { PALETTE_CATEGORICA } from '../lib/chartColors'
import { formattatoreAsseCompatto } from '../lib/numberFormat'
import { fontDisplay, fontMono, tokens } from '../theme'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

const ANNO_CORRENTE = new Date().getFullYear()

const TESTO_ESITO: Record<EsitoIntegrazione, string> = {
  [EsitoIntegrazione.NonConcesso]: 'non concesso',
  [EsitoIntegrazione.NonConfigurato]: 'non configurato',
  [EsitoIntegrazione.Attesa]: 'in attesa',
  [EsitoIntegrazione.Errore]: 'errore',
  [EsitoIntegrazione.Attivo]: 'attivo',
}

const COLORE_ESITO: Record<EsitoIntegrazione, string> = {
  [EsitoIntegrazione.NonConcesso]: tokens.textTertiary,
  [EsitoIntegrazione.NonConfigurato]: tokens.textTertiary,
  [EsitoIntegrazione.Attesa]: tokens.wait600,
  [EsitoIntegrazione.Errore]: tokens.error600,
  [EsitoIntegrazione.Attivo]: tokens.ok600,
}

export function StatisticheSuperAdminPage() {
  const { isSuperAdmin } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniDisponibiliStatisticheSuperAdmin(isSuperAdmin)
  const anniSelezionabili = anniDisponibili.data && anniDisponibili.data.length > 0 ? anniDisponibili.data : [ANNO_CORRENTE]

  // Su richiesta esplicita, il selettore propone solo anni con dati reali.
  useEffect(() => {
    if (anniDisponibili.data && anniDisponibili.data.length > 0 && !anniDisponibili.data.includes(anno)) {
      setAnno(anniDisponibili.data[0])
    }
  }, [anniDisponibili.data]) // eslint-disable-line react-hooks/exhaustive-deps

  const statistiche = useStatisticheSuperAdmin(isSuperAdmin, anno)

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  if (statistiche.isLoading) {
    return <Skeleton variant="rounded" height={480} />
  }

  if (statistiche.isError || !statistiche.data) {
    return <Alert severity="error">Impossibile caricare le statistiche. Riprova.</Alert>
  }

  const dati = statistiche.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 18 }}>Statistiche</Typography>
        <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {anniSelezionabili.map((a) => (
            <MenuItem key={a} value={a}>
              {a}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(4, 1fr)' }, gap: 2 }}>
        <KpiCard etichetta="Clienti" valore={String(dati.panoramica.clientiTotali)} dettaglio={`${dati.panoramica.clientiAttivi} attivi`} />
        <KpiCard etichetta="Strutture" valore={String(dati.panoramica.struttureTotali)} dettaglio={`${dati.panoramica.struttureAttive} attive`} />
        <KpiCard
          etichetta="Incasso totale (anno)"
          valore={formattatoreValuta.format(dati.incassiPerCliente.reduce((tot, c) => tot + c.importoPagatoAnno, 0))}
          accento
        />
        <KpiCard
          etichetta="Integrazioni in errore"
          valore={String(dati.saluteIntegrazioni.filter((s) => haErrore(s)).length)}
          dettaglio="strutture da controllare"
          accento={dati.saluteIntegrazioni.some((s) => haErrore(s))}
          coloreAccento="error"
        />
      </Box>

      <CardGrafico titolo="Nuovi clienti per mese">
        <BarChart
          height={240}
          hideLegend
          borderRadius={4}
          xAxis={[{ data: NOMI_MESI, scaleType: 'band' }]}
          yAxis={[{ valueFormatter: formattatoreAsseCompatto(String) }]}
          series={[{ data: dati.nuoviClientiPerMese.map((v) => v.conteggio), label: 'Nuovi clienti', color: PALETTE_CATEGORICA[0] }]}
        />
      </CardGrafico>

      <CardGrafico titolo="Incassi per cliente">
        {dati.incassiPerCliente.length === 0 ? (
          <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessun incasso registrato per l'anno selezionato.</Typography>
        ) : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Cliente</TableCell>
                  <TableCell align="right">Strutture</TableCell>
                  <TableCell align="right">Incasso (pagato)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {dati.incassiPerCliente.map((c) => (
                  <TableRow key={c.clienteId} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{c.ragioneSociale}</TableCell>
                    <TableCell align="right" sx={{ fontFamily: fontMono }}>
                      {c.numeroStrutture}
                    </TableCell>
                    <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                      {formattatoreValuta.format(c.importoPagatoAnno)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </CardGrafico>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 2.5, alignItems: 'start' }}>
        <CardGrafico titolo="Top 10 strutture per fatturato">
          {dati.classificaStruttureFatturato.length === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessun dato per l'anno selezionato.</Typography>
          ) : (
            <BarChart
              layout="horizontal"
              height={Math.max(220, dati.classificaStruttureFatturato.length * 40)}
              hideLegend
              borderRadius={4}
              yAxis={[{ data: dati.classificaStruttureFatturato.map((s) => s.nomeStruttura), scaleType: 'band' }]}
              xAxis={[{ valueFormatter: formattatoreAsseCompatto((v) => formattatoreValuta.format(v)) }]}
              series={[
                {
                  data: dati.classificaStruttureFatturato.map((s) => s.valore),
                  label: 'Fatturato',
                  color: PALETTE_CATEGORICA[0],
                  valueFormatter: (v) => formattatoreValuta.format(v ?? 0),
                },
              ]}
            />
          )}
        </CardGrafico>

        <CardGrafico titolo="Top 10 strutture per occupazione">
          {dati.classificaStruttureOccupazione.length === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessun dato per l'anno selezionato.</Typography>
          ) : (
            <BarChart
              layout="horizontal"
              height={Math.max(220, dati.classificaStruttureOccupazione.length * 40)}
              hideLegend
              borderRadius={4}
              yAxis={[{ data: dati.classificaStruttureOccupazione.map((s) => s.nomeStruttura), scaleType: 'band' }]}
              series={[
                {
                  data: dati.classificaStruttureOccupazione.map((s) => s.valore),
                  label: 'Occupazione',
                  color: PALETTE_CATEGORICA[1],
                  valueFormatter: (v) => `${v ?? 0}%`,
                },
              ]}
            />
          )}
        </CardGrafico>
      </Box>

      <CardGrafico titolo="Salute integrazioni">
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Struttura</TableCell>
                <TableCell>Cliente</TableCell>
                <TableCell>Alloggiati Web</TableCell>
                <TableCell>Osservatorio</TableCell>
                <TableCell>PayTourist</TableCell>
                <TableCell>Wubook</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dati.saluteIntegrazioni.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna struttura.
                  </TableCell>
                </TableRow>
              )}
              {dati.saluteIntegrazioni.map((s) => (
                <TableRow key={s.strutturaId} hover>
                  <TableCell sx={{ fontWeight: 600 }}>{s.nomeStruttura}</TableCell>
                  <TableCell sx={{ color: tokens.textSecondary }}>{s.ragioneSocialeCliente}</TableCell>
                  <TableCell>
                    <PallinoEsito esito={s.alloggiatiWeb} />
                  </TableCell>
                  <TableCell>
                    <PallinoEsito esito={s.osservatorio} />
                  </TableCell>
                  <TableCell>
                    <PallinoEsito esito={s.payTourist} />
                  </TableCell>
                  <TableCell>
                    <PallinoEsito esito={s.wubook} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </CardGrafico>
    </Box>
  )
}

function haErrore(s: { alloggiatiWeb: EsitoIntegrazioneDto; osservatorio: EsitoIntegrazioneDto; payTourist: EsitoIntegrazioneDto; wubook: EsitoIntegrazioneDto }) {
  return [s.alloggiatiWeb, s.osservatorio, s.payTourist, s.wubook].some((e) => e.stato === EsitoIntegrazione.Errore)
}

function PallinoEsito({ esito }: { esito: EsitoIntegrazioneDto }) {
  const contenuto = (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
      <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: COLORE_ESITO[esito.stato] }} />
      <Typography sx={{ fontSize: 12, color: tokens.textSecondary }}>{TESTO_ESITO[esito.stato]}</Typography>
    </Box>
  )

  if (!esito.ultimoErrore && !esito.ultimoInvioAtUtc) {
    return contenuto
  }

  const titolo = esito.ultimoErrore ?? `Ultimo invio: ${formattatoreDataOra.format(new Date(esito.ultimoInvioAtUtc!))}`
  return <Tooltip title={titolo}>{contenuto}</Tooltip>
}

function CardGrafico({ titolo, children }: { titolo: string; children: React.ReactNode }) {
  return (
    <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>{titolo}</Typography>
      {children}
    </Box>
  )
}
