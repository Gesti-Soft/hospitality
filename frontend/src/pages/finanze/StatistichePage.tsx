import { useEffect, useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { PieChart } from '@mui/x-charts/PieChart'
import { LineChart } from '@mui/x-charts/LineChart'
import { BarChart } from '@mui/x-charts/BarChart'
import BarChartIcon from '@mui/icons-material/BarChartRounded'
import EuroIcon from '@mui/icons-material/EuroRounded'
import AccessTimeIcon from '@mui/icons-material/AccessTimeRounded'
import HomeIcon from '@mui/icons-material/HomeRounded'
import PercentIcon from '@mui/icons-material/PercentRounded'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useAnniDisponibiliStatistiche, useStatisticheStruttura } from '../../api/statistiche'
import { KpiCard } from '../../components/KpiCard'
import { MatriceTipologieMese, NOMI_MESI } from '../../components/statistiche/MatriceTipologieMese'
import { coloriPerEtichette, PALETTE_CATEGORICA } from '../../lib/chartColors'
import { formattatoreAsseCompatto } from '../../lib/numberFormat'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { fontDisplay, tokens } from '../../theme'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

// Sopra questa soglia un grafico a barre con una serie per tipologia diventa illeggibile — la
// matrice completa (tutte le tipologie, non solo le prime) resta comunque disponibile in tabella.
const MASSIMO_TIPOLOGIE_PER_GRAFICO = 6

// Sotto questa larghezza della card la legenda di fianco non lascia spazio al disegno: le etichette
// delle nazionalità sono lunghe ("STATI UNITI D'AMERICA — 7.1%") e si prendono oltre 260 px, così a
// 1210 px di finestra la ciambella si riduceva a 180 px. Si misura la card e non la finestra perché
// la stessa finestra dà card larghe o strette a seconda che la griglia sia a una o due colonne.
const LARGHEZZA_MINIMA_PER_LEGENDA_A_LATO = 560

export function StatistichePage() {
  const { strutturaId } = useStruttura()
  const mobile = useMobile()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniDisponibiliStatistiche(strutturaId)
  // Su richiesta esplicita, il selettore propone gli anni con dati reali più l'anno corrente,
  // sempre — anche una struttura appena creata deve poterlo selezionare.
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)

  const statistiche = useStatisticheStruttura(strutturaId, anno)

  // Le due torte stanno nella stessa colonna della griglia, quindi hanno sempre la stessa larghezza:
  // ne basta misurare una. Finché la misura non è arrivata si parte dalla finestra, così su un
  // telefono la prima pennellata è già quella giusta e non si vede il salto.
  const rifTorta = useRef<HTMLDivElement | null>(null)
  const [larghezzaTorta, setLarghezzaTorta] = useState(0)
  // Le card dei grafici nascono solo quando i dati arrivano: prima di allora non c'è niente da misurare.
  const graficiPresenti = statistiche.data !== undefined
  useEffect(() => {
    const nodo = rifTorta.current
    if (!nodo) {
      return
    }
    const osservatore = new ResizeObserver(([voce]) => setLarghezzaTorta(voce.contentRect.width))
    osservatore.observe(nodo)
    return () => osservatore.disconnect()
  }, [graficiPresenti])

  // La legenda sta di default a destra del disegno e si prende la larghezza che le serve: quando la
  // card è stretta la ciambella si rimpicciolisce fino a diventare illeggibile. Sotto la soglia la
  // legenda passa sotto, così il disegno tiene tutta la larghezza e resta centrato; l'altezza cresce
  // perché la legenda non gli mangi lo spazio appena guadagnato.
  const legendaSotto = larghezzaTorta > 0 ? larghezzaTorta < LARGHEZZA_MINIMA_PER_LEGENDA_A_LATO : mobile
  const legendaTorta = legendaSotto
    ? { legend: { direction: 'horizontal' as const, position: { vertical: 'bottom' as const, horizontal: 'center' as const } } }
    : undefined
  const altezzaTorta = legendaSotto ? 340 : 280
  const dati = statistiche.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'end' }}>
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
          {/* minmax(0, …) e non 1fr: una colonna 1fr non scende sotto il contenuto, e un importo a
              sette cifre allargava la griglia oltre la finestra, facendo comparire lo scorrimento
              orizzontale su tutta la pagina invece di mandare i riquadri a capo. */}
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(3, minmax(0, 1fr))' }, gap: 2 }}>
            <KpiCard etichetta="Prenotazioni anno" valore={String(dati.kpi.numeroPrenotazioni)} icona={<BarChartIcon />} />
            <KpiCard etichetta="Ricavo stimato" valore={formattatoreValuta.format(dati.kpi.ricavoStimato)} icona={<EuroIcon />} />
            <KpiCard etichetta="Ricavo effettivo" valore={formattatoreValuta.format(dati.kpi.ricavoEffettivo)} accento icona={<EuroIcon />} />
            <KpiCard
              etichetta="Permanenza media"
              valore={dati.kpi.permanenzaMediaNotti === null ? '—' : dati.kpi.permanenzaMediaNotti.toFixed(1)}
              dettaglio={dati.kpi.permanenzaMediaNotti === null ? undefined : 'notti'}
              icona={<AccessTimeIcon />}
            />
            <KpiCard etichetta="Tasso occupazione" valore={`${dati.kpi.tassoOccupazionePercentuale}%`} icona={<HomeIcon />} />
            <KpiCard etichetta="Tassa di soggiorno" valore={formattatoreValuta.format(dati.tassaSoggiorno.totaleAnno)} icona={<PercentIcon />} />
          </Box>

          {dati.kpi.numeroPrenotazioni === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna prenotazione per l'anno selezionato.</Typography>
          ) : (
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 2.5, alignItems: 'start' }}>
              <CardGrafico titolo="Prenotazioni per agenzia">
                {/* Il riferimento serve a misurare la larghezza reale della card (vedi legendaSotto). */}
                <Box ref={rifTorta}>
                  <PieChart
                    height={altezzaTorta}
                    slotProps={legendaTorta}
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
                </Box>
              </CardGrafico>

              <CardGrafico titolo="Prenotazioni per nazionalità">
                <PieChart
                  height={altezzaTorta}
                  slotProps={legendaTorta}
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

              <CardGrafico titolo="Tassa di soggiorno mensile">
                <BarChart
                  height={260}
                  hideLegend
                  borderRadius={4}
                  xAxis={[{ data: NOMI_MESI, scaleType: 'band' }]}
                  yAxis={[{ valueFormatter: formattatoreAsseCompatto((v) => formattatoreValuta.format(v)) }]}
                  series={[
                    {
                      data: dati.tassaSoggiorno.andamentoMensile.map((v) => v.valore),
                      label: 'Tassa di soggiorno',
                      color: PALETTE_CATEGORICA[1],
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
