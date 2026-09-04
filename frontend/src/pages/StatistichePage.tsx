import { useEffect, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { PieChart } from '@mui/x-charts/PieChart'
import { LineChart } from '@mui/x-charts/LineChart'
import { BarChart } from '@mui/x-charts/BarChart'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAnniDisponibiliStatistiche, useStatisticheStruttura } from '../api/statistiche'
import { KpiCard } from '../components/KpiCard'
import { MatriceTipologieMese, NOMI_MESI } from '../components/statistiche/MatriceTipologieMese'
import { coloriPerEtichette, PALETTE_CATEGORICA } from '../lib/chartColors'
import { formattatoreAsseCompatto } from '../lib/numberFormat'
import { fontDisplay, tokens } from '../theme'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

const ANNO_CORRENTE = new Date().getFullYear()

// Sopra questa soglia un grafico a barre con una serie per tipologia diventa illeggibile — la
// matrice completa (tutte le tipologie, non solo le prime) resta comunque disponibile in tabella.
const MASSIMO_TIPOLOGIE_PER_GRAFICO = 6

export function StatistichePage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniDisponibiliStatistiche(strutturaId)
  // Nessun anno con dati (struttura appena creata): l'anno corrente resta comunque selezionabile,
  // così la pagina mostra lo stato vuoto ("Nessuna prenotazione...") invece di un selettore vuoto.
  const anniSelezionabili = anniDisponibili.data && anniDisponibili.data.length > 0 ? anniDisponibili.data : [ANNO_CORRENTE]

  // Su richiesta esplicita, il selettore propone solo anni con dati reali (mai 2027 o un anno
  // passato sicuramente vuoto) — appena la lista arriva, se l'anno corrente non ne fa parte si
  // passa al più recente con dati.
  useEffect(() => {
    if (anniDisponibili.data && anniDisponibili.data.length > 0 && !anniDisponibili.data.includes(anno)) {
      setAnno(anniDisponibili.data[0])
    }
  }, [anniDisponibili.data]) // eslint-disable-line react-hooks/exhaustive-deps

  const statistiche = useStatisticheStruttura(strutturaId, anno)
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

      {statistiche.isLoading && <Skeleton variant="rounded" height={480} />}
      {statistiche.isError && <Alert severity="error">Impossibile caricare le statistiche. Riprova.</Alert>}

      {dati && (
        <>
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
            <KpiCard etichetta="Prenotazioni anno" valore={String(dati.kpi.numeroPrenotazioni)} />
            <KpiCard etichetta="Ricavo stimato" valore={formattatoreValuta.format(dati.kpi.ricavoStimato)} dettaglio="importo totale prenotazioni" />
            <KpiCard etichetta="Ricavo effettivo" valore={formattatoreValuta.format(dati.kpi.ricavoEffettivo)} dettaglio="importo pagato" accento />
            <KpiCard
              etichetta="Permanenza media"
              valore={dati.kpi.permanenzaMediaNotti === null ? '—' : dati.kpi.permanenzaMediaNotti.toFixed(1)}
              dettaglio={dati.kpi.permanenzaMediaNotti === null ? undefined : 'notti'}
            />
            <KpiCard etichetta="Tasso occupazione" valore={`${dati.kpi.tassoOccupazionePercentuale}%`} />
            <KpiCard etichetta="Tassa di soggiorno" valore={formattatoreValuta.format(dati.tassaSoggiorno.totaleAnno)} dettaglio="totale anno" />
          </Box>

          {dati.kpi.numeroPrenotazioni === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna prenotazione per l'anno selezionato.</Typography>
          ) : (
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 2.5, alignItems: 'start' }}>
              <CardGrafico titolo="Prenotazioni per agenzia">
                <PieChart
                  height={280}
                  series={[
                    {
                      innerRadius: '58%',
                      cornerRadius: 6,
                      paddingAngle: 2,
                      highlightScope: { highlight: 'item', fade: 'none' },
                      highlighted: { additionalRadius: 6 },
                      data: dati.prenotazioniPerAgenzia.map((v) => ({ id: v.etichetta, value: v.conteggio, label: v.etichetta })),
                      valueFormatter: (v) => `${v.value} prenotazioni`,
                    },
                  ]}
                  colors={coloriPerEtichette(dati.prenotazioniPerAgenzia.map((v) => v.etichetta))}
                />
              </CardGrafico>

              <CardGrafico titolo="Prenotazioni per nazionalità">
                <PieChart
                  height={280}
                  series={[
                    {
                      innerRadius: '58%',
                      cornerRadius: 6,
                      paddingAngle: 2,
                      highlightScope: { highlight: 'item', fade: 'none' },
                      highlighted: { additionalRadius: 6 },
                      data: dati.prenotazioniPerNazionalita.map((v) => ({
                        id: v.etichetta,
                        value: v.conteggio,
                        label: `${v.etichetta} — ${v.percentuale}%`,
                      })),
                      valueFormatter: (v) => `${v.value} prenotazioni`,
                    },
                  ]}
                  colors={coloriPerEtichette(dati.prenotazioniPerNazionalita.map((v) => v.etichetta))}
                />
              </CardGrafico>

              <CardGrafico titolo="Andamento ricavo mensile">
                <LineChart
                  height={260}
                  hideLegend
                  sx={{ '& .MuiLineChart-area': { fillOpacity: 0.14 } }}
                  xAxis={[{ data: NOMI_MESI, scaleType: 'point' }]}
                  yAxis={[{ valueFormatter: formattatoreAsseCompatto((v) => formattatoreValuta.format(v)) }]}
                  series={[
                    {
                      data: dati.andamentoRicavoMensile.map((v) => v.valore),
                      label: 'Ricavo pagato',
                      color: PALETTE_CATEGORICA[0],
                      curve: 'monotoneX',
                      area: true,
                      showMark: true,
                      valueFormatter: (v) => formattatoreValuta.format(v ?? 0),
                    },
                  ]}
                />
              </CardGrafico>

              <CardGrafico titolo="Ricavo per tipologia camera">
                <BarChart
                  layout="horizontal"
                  height={Math.max(220, dati.ricavoPerTipologiaCamera.length * 44)}
                  hideLegend
                  borderRadius={4}
                  yAxis={[{ data: dati.ricavoPerTipologiaCamera.map((v) => v.etichetta), scaleType: 'band' }]}
                  xAxis={[{ valueFormatter: formattatoreAsseCompatto((v) => formattatoreValuta.format(v)) }]}
                  series={[
                    {
                      data: dati.ricavoPerTipologiaCamera.map((v) => v.importo),
                      label: 'Ricavo (pagato)',
                      color: PALETTE_CATEGORICA[0],
                      valueFormatter: (v) => formattatoreValuta.format(v ?? 0),
                    },
                  ]}
                />
              </CardGrafico>

              <CardGrafico titolo="Prenotazioni per tipologia camera / mese">
                {dati.prenotazioniPerTipologiaMese.tipologie.length <= MASSIMO_TIPOLOGIE_PER_GRAFICO ? (
                  <BarChart
                    height={320}
                    borderRadius={4}
                    xAxis={[{ data: NOMI_MESI, scaleType: 'band' }]}
                    yAxis={[{ valueFormatter: formattatoreAsseCompatto(String) }]}
                    series={dati.prenotazioniPerTipologiaMese.tipologie.map((tipologia, i) => ({
                      data: dati.prenotazioniPerTipologiaMese.righe.map((r) => r.conteggiPerTipologia[i]),
                      label: tipologia,
                      color: coloriPerEtichette(dati.prenotazioniPerTipologiaMese.tipologie)[i],
                    }))}
                  />
                ) : (
                  <MatriceTipologieMese matrice={dati.prenotazioniPerTipologiaMese} />
                )}
              </CardGrafico>
            </Box>
          )}
        </>
      )}
    </Box>
  )
}

function CardGrafico({ titolo, children }: { titolo: string; children: React.ReactNode }) {
  return (
    <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>{titolo}</Typography>
      {children}
    </Box>
  )
}
