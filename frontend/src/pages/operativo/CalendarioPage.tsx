import { useEffect, useMemo, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import Skeleton from '@mui/material/Skeleton'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import CalendarIcon from '@mui/icons-material/CalendarTodayOutlined'
import SearchIcon from '@mui/icons-material/Search'
import { useStruttura } from '../../struttura/StrutturaContext'
import { StatoCamera, useCamere, type CameraDto } from '../../api/camere'
import {
  useAgenzieDistinct,
  useArriviInCorso,
  useArriviProssimi,
  usePrenotazioniPeriodo,
  useStoricoPrenotazioni,
  StatoPrenotazione,
  type PrenotazioneDto,
} from '../../api/prenotazioni'
import { useCanaliVendita } from '../../api/canaliVendita'
import { useTipologie } from '../../api/tipologie'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { PALETTE_CANALI } from '../../lib/coloriCanali'
import { aggiungiGiorni, differenzaGiorni, formatoInputData, inizioGiornoLocale, parsaInputData } from '../../lib/date'
import { confrontaNaturale } from '../../lib/ordinamento'
import { useMobile } from '../../lib/useMobile'
import { PrenotazioneDialog, type StatoIniziale, ETICHETTA_STATO, COLORE_STATO } from '../../components/PrenotazioneDialog'
import { BottoneNuovo, CardElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { CalendarioPopover } from '../../components/CalendarioPopover'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

const GIORNI_VISIBILI_DEFAULT = 14
const GIORNI_VISIBILI_MIN = 7
const COL_CAMERA = 184
const COL_GIORNO = 74
const RIGA_ALTEZZA = 56

const NOMI_GIORNO = ['DOM', 'LUN', 'MAR', 'MER', 'GIO', 'VEN', 'SAB']
const FORMATTATORE_LABEL = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short', year: 'numeric' })
const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

type FormatoCalendario = 'griglia' | 'lista'
type VistaLista = 'arrivi' | 'in-corso' | 'storico'

function normalizzaAgenzia(agenzia: string | null): string {
  const a = (agenzia ?? '').trim()
  return a === '' ? 'Diretta' : a
}

/**
 * Colore di un canale: preso dal colore configurato in Canali vendita quando esiste una
 * corrispondenza (match case-insensitive sulla descrizione, l'agenzia sulla prenotazione resta
 * testo libero), altrimenti dalla palette in base alla posizione nell'elenco STABILE di tutte le
 * agenzie della struttura (`useAgenzieDistinct`, non filtrato per periodo). Bug corretto: prima
 * l'indice veniva calcolato sulle sole agenzie presenti nel periodo/vista visibile in quel momento,
 * quindi cambiando pagina o navigando ad un periodo senza prenotazioni di un canale i colori di
 * TUTTI i canali si spostavano (l'indice di ognuno cambiava). Con un elenco stabile — o meglio,
 * con il colore fisso persistito sul canale — lo stesso canale ha sempre lo stesso colore ovunque.
 */
function coloreCanale(agenzia: string | null, mappaColoriCanali: Map<string, string>, agenzieStabili: string[]): { colore: string; etichetta: string } {
  const etichetta = normalizzaAgenzia(agenzia)
  const coloreConfigurato = mappaColoriCanali.get(etichetta.toLowerCase())
  if (coloreConfigurato) return { colore: coloreConfigurato, etichetta }
  const idx = Math.max(0, agenzieStabili.indexOf(etichetta))
  return { colore: PALETTE_CANALI[idx % PALETTE_CANALI.length], etichetta }
}

const ETICHETTA_STATO_CAMERA: Record<StatoCamera, string> = {
  [StatoCamera.Pronta]: 'Pronta',
  [StatoCamera.Occupata]: 'Occupata',
  [StatoCamera.DaPulire]: 'Da pulire',
  [StatoCamera.NonDisponibile]: 'Non disponibile',
}

const COLORE_STATO_CAMERA: Record<StatoCamera, string> = {
  [StatoCamera.Pronta]: tokens.ok600,
  [StatoCamera.Occupata]: tokens.blue600,
  [StatoCamera.DaPulire]: tokens.wait600,
  [StatoCamera.NonDisponibile]: tokens.error600,
}

export function CalendarioPage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoScrivere = usePuoScrivere('reservationWrite')
  const [inizioFinestra, setInizioFinestra] = useState(() => inizioGiornoLocale(new Date()))
  const [anchorElCalendario, setAnchorElCalendario] = useState<HTMLElement | null>(null)
  const [formato, setFormato] = useState<FormatoCalendario>('griglia')
  const [vistaLista, setVistaLista] = useState<VistaLista>('arrivi')
  const [dialogo, setDialogo] = useState<StatoIniziale | null>(null)
  const [filtroTipologiaId, setFiltroTipologiaId] = useState('')
  const [filtroAgenzia, setFiltroAgenzia] = useState('')
  const [ricerca, setRicerca] = useState('')

  // La griglia occupa tutto lo spazio disponibile: il numero di giorni visibili si ricalcola in
  // base alla larghezza del contenitore, non è più un valore fisso.
  const contenitoreRef = useRef<HTMLDivElement>(null)
  const [larghezzaContenitore, setLarghezzaContenitore] = useState(0)

  useEffect(() => {
    const el = contenitoreRef.current
    if (!el) return
    const observer = new ResizeObserver((entries) => setLarghezzaContenitore(entries[0].contentRect.width))
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const giorniVisibili =
    larghezzaContenitore > 0
      ? Math.max(GIORNI_VISIBILI_MIN, Math.floor((larghezzaContenitore - COL_CAMERA) / COL_GIORNO))
      : GIORNI_VISIBILI_DEFAULT

  const fineFinestra = useMemo(() => aggiungiGiorni(inizioFinestra, giorniVisibili), [inizioFinestra, giorniVisibili])
  const giorni = useMemo(
    () => Array.from({ length: giorniVisibili }, (_, i) => aggiungiGiorni(inizioFinestra, i)),
    [inizioFinestra, giorniVisibili],
  )

  const camere = useCamere(strutturaId)
  const prenotazioni = usePrenotazioniPeriodo(strutturaId, inizioFinestra, fineFinestra)
  const canali = useCanaliVendita(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const agenzieOpzioni = useAgenzieDistinct(strutturaId)

  // In vista Calendario non c'è ricerca (la navigazione è per data, campo cerca nascosto) — il
  // filtro camere qui è solo per Tipologia. In vista Lista la ricerca cerca dentro le prenotazioni
  // (ospite, importo, data...), vedi testoRicercaLista più sotto.
  const gruppi = useMemo(() => {
    if (!camere.data) return []
    const filtrate = camere.data.filter((c) => {
      if (filtroTipologiaId && c.tipologiaId !== filtroTipologiaId) return false
      return true
    })
    const mappa = new Map<string, CameraDto[]>()
    for (const c of filtrate) {
      const chiave = c.tipologiaNome ?? 'Senza tipologia'
      if (!mappa.has(chiave)) mappa.set(chiave, [])
      mappa.get(chiave)!.push(c)
    }
    return Array.from(mappa.entries())
      .sort(([a], [b]) => confrontaNaturale(a, b))
      .map(([tipologia, elenco]) => ({ tipologia, camere: elenco.sort((x, y) => confrontaNaturale(x.nome, y.nome)) }))
  }, [camere.data, filtroTipologiaId])

  const prenotazioniFiltrate = useMemo(() => {
    const dati = prenotazioni.data ?? []
    return filtroAgenzia ? dati.filter((p) => normalizzaAgenzia(p.agenzia) === filtroAgenzia) : dati
  }, [prenotazioni.data, filtroAgenzia])

  const prenotazioniPerCamera = useMemo(() => {
    const mappa = new Map<string, PrenotazioneDto[]>()
    for (const p of prenotazioniFiltrate) {
      if (!p.cameraId || !p.checkIn || !p.checkOut) continue
      if (!mappa.has(p.cameraId)) mappa.set(p.cameraId, [])
      mappa.get(p.cameraId)!.push(p)
    }
    return mappa
  }, [prenotazioniFiltrate])

  // Chi mostrare in legenda: solo i canali con almeno una prenotazione nel periodo/vista corrente.
  const agenzieDistinct = useMemo(
    () => Array.from(new Set(prenotazioniFiltrate.map((p) => normalizzaAgenzia(p.agenzia)))).sort(),
    [prenotazioniFiltrate],
  )

  // Colore di ciascun canale: dalla configurazione in Canali vendita quando esiste, altrimenti da
  // un elenco STABILE (tutte le agenzie della struttura, non filtrato per periodo) — vedi coloreCanale.
  const mappaColoriCanali = useMemo(() => {
    const mappa = new Map<string, string>()
    for (const c of canali.data ?? []) mappa.set(normalizzaAgenzia(c.descrizione).toLowerCase(), c.colore)
    return mappa
  }, [canali.data])
  const agenzieStabili = useMemo(() => (agenzieOpzioni.data ?? []).map(normalizzaAgenzia), [agenzieOpzioni.data])

  // Vista Lista: non è legata al periodo scelto per la griglia (quello serve solo alla vista
  // Calendario) — sfoglia per Arrivi/In corso/Storico come la pagina Ospiti, con gli stessi filtri
  // Tipologia/camera cercata/Agenzia applicati sopra.
  const arrivi = useArriviProssimi(strutturaId)
  const inCorsoLista = useArriviInCorso(strutturaId)
  const storico = useStoricoPrenotazioni(strutturaId, new Date().getFullYear())
  const datiVistaLista = vistaLista === 'arrivi' ? arrivi.data : vistaLista === 'in-corso' ? inCorsoLista.data : storico.data
  const caricamentoLista = vistaLista === 'arrivi' ? arrivi.isLoading : vistaLista === 'in-corso' ? inCorsoLista.isLoading : storico.isLoading

  const idCamereFiltrate = useMemo(() => new Set(gruppi.flatMap((g) => g.camere.map((c) => c.id))), [gruppi])
  const testoRicercaLista = formato === 'lista' ? ricerca.trim().toLowerCase() : ''
  const prenotazioniLista = useMemo(() => {
    const dati = datiVistaLista ?? []
    return dati.filter((p) => {
      if (filtroAgenzia && normalizzaAgenzia(p.agenzia) !== filtroAgenzia) return false
      if (p.cameraId && !idCamereFiltrate.has(p.cameraId)) return false
      if (testoRicercaLista) {
        const campi = [
          p.ospiteNome,
          p.ospiteCognome,
          p.cameraNome,
          p.numeroPrenotazione != null ? `#${p.numeroPrenotazione}` : null,
          normalizzaAgenzia(p.agenzia),
          p.checkIn ? FORMATTATORE_LABEL.format(new Date(p.checkIn)) : null,
          p.checkOut ? FORMATTATORE_LABEL.format(new Date(p.checkOut)) : null,
          p.importoTotale != null ? formattatoreValuta.format(p.importoTotale) : null,
          p.importoPagato != null ? formattatoreValuta.format(p.importoPagato) : null,
        ]
          .filter(Boolean)
          .join(' ')
          .toLowerCase()
        if (!campi.includes(testoRicercaLista)) return false
      }
      return true
    })
  }, [datiVistaLista, filtroAgenzia, idCamereFiltrate, testoRicercaLista])

  const caricamento = formato === 'lista' ? camere.isLoading || caricamentoLista : camere.isLoading || prenotazioni.isLoading

  return (
    <Box ref={contenitoreRef} sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, width: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        {formato === 'griglia' ? (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, mobile ? -1 : -giorniVisibili))}>
              <ChevronLeftIcon fontSize="small" />
            </IconButton>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 14.5, px: 0.5, minWidth: mobile ? 130 : 190, textAlign: 'center' }}>
              {mobile ? FORMATTATORE_LABEL.format(giorni[0]) : `${FORMATTATORE_LABEL.format(giorni[0])} – ${FORMATTATORE_LABEL.format(giorni[giorni.length - 1])}`}
            </Typography>
            <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, mobile ? 1 : giorniVisibili))}>
              <ChevronRightIcon fontSize="small" />
            </IconButton>
            <IconButton size="small" onClick={(e) => setAnchorElCalendario(e.currentTarget)} title="Scegli un periodo">
              <CalendarIcon fontSize="small" />
            </IconButton>
            <CalendarioPopover
              anchorEl={anchorElCalendario}
              valore={formatoInputData(inizioFinestra)}
              mostraOggi
              onSeleziona={(valore) => {
                setInizioFinestra(inizioGiornoLocale(parsaInputData(valore)))
                setAnchorElCalendario(null)
              }}
              onClose={() => setAnchorElCalendario(null)}
            />
          </Box>
        ) : (
          <Tabs value={vistaLista} onChange={(_, v) => setVistaLista(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ minHeight: 0 }}>
            <Tab label="Arrivi" value="arrivi" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
            <Tab label="In corso" value="in-corso" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
            <Tab label="Storico" value="storico" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          </Tabs>
        )}

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <ToggleButtonGroup exclusive size="small" value={formato} onChange={(_, v) => v && setFormato(v)}>
            <ToggleButton value="griglia">Calendario</ToggleButton>
            <ToggleButton value="lista">Lista</ToggleButton>
          </ToggleButtonGroup>
          {formato === 'griglia' && <Legenda agenzieDistinct={agenzieDistinct} mappaColoriCanali={mappaColoriCanali} agenzieStabili={agenzieStabili} />}
          {puoScrivere && (
            <BottoneNuovo
              etichetta="+ Nuova prenotazione"
              size="medium"
              disabilitato={!strutturaId || !camere.data || camere.data.length === 0}
              onClick={() =>
                setDialogo({
                  modo: 'crea',
                  cameraId: null,
                  checkIn: inizioGiornoLocale(new Date()),
                  checkOut: aggiungiGiorni(new Date(), 1),
                })
              }
            />
          )}
        </Box>
      </Box>

      {!caricamento && camere.data && camere.data.length > 0 && (
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: mobile ? 'flex-start' : 'flex-end', gap: 1.5, flexWrap: 'wrap' }}>
          <TextField select size="small" label="Tipologia" value={filtroTipologiaId} onChange={(e) => setFiltroTipologiaId(e.target.value)} sx={{ minWidth: 180 }}>
            <MenuItem value="">Tutte</MenuItem>
            {(tipologie.data ?? []).map((t) => (
              <MenuItem key={t.id} value={t.id}>
                {t.tipologiaCamera}
              </MenuItem>
            ))}
          </TextField>
          {(agenzieOpzioni.data ?? []).length > 0 && (
            <TextField select size="small" label="Agenzia" value={filtroAgenzia} onChange={(e) => setFiltroAgenzia(e.target.value)} sx={{ minWidth: 160 }}>
              <MenuItem value="">Tutte</MenuItem>
              {(agenzieOpzioni.data ?? []).map((a) => (
                <MenuItem key={a} value={a}>
                  {a}
                </MenuItem>
              ))}
            </TextField>
          )}
          {formato === 'lista' && (
            <TextField
              size="small"
              placeholder="Cerca ospite, camera, data, importo..."
              value={ricerca}
              onChange={(e) => setRicerca(e.target.value)}
              sx={{ minWidth: 220 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" sx={{ color: tokens.textTertiary }} />
                    </InputAdornment>
                  ),
                },
              }}
            />
          )}
        </Box>
      )}

      {caricamento && <Skeleton variant="rounded" height={420} />}

      {!caricamento && camere.data && camere.data.length === 0 && (
        <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 6, textAlign: 'center', color: tokens.textSecondary }}>
          Nessuna camera configurata per questa struttura. Aggiungila dalla sezione Camere prima di poter creare prenotazioni.
        </Box>
      )}

      {!caricamento && camere.data && camere.data.length > 0 && gruppi.length === 0 && (
        <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 6, textAlign: 'center', color: tokens.textSecondary }}>
          Nessuna camera corrisponde ai filtri selezionati.
        </Box>
      )}

      {!caricamento && formato === 'griglia' && gruppi.length > 0 && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          {gruppi.map((gruppo) => (
            <Box key={gruppo.tipologia} sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textSecondary, textTransform: 'uppercase', letterSpacing: '.06em' }}>
                {gruppo.tipologia}
              </Typography>
              {gruppo.camere.map((camera) => {
                const prenotazioneGiorno = (prenotazioniPerCamera.get(camera.id) ?? []).find((p) => {
                  const ci = inizioGiornoLocale(new Date(p.checkIn!))
                  const co = inizioGiornoLocale(new Date(p.checkOut!))
                  return giorni[0] >= ci && giorni[0] < co
                })
                const nomeOspite = prenotazioneGiorno
                  ? prenotazioneGiorno.ospiteNome || prenotazioneGiorno.ospiteCognome
                    ? `${prenotazioneGiorno.ospiteNome ?? ''} ${prenotazioneGiorno.ospiteCognome ?? ''}`.trim()
                    : null
                  : null
                const { colore: coloreCanaleGiorno, etichetta: etichettaCanale } = prenotazioneGiorno
                  ? coloreCanale(prenotazioneGiorno.agenzia, mappaColoriCanali, agenzieStabili)
                  : { colore: undefined, etichetta: undefined }

                return (
                  <CardElenco
                    key={camera.id}
                    coloreAccento={coloreCanaleGiorno}
                    onClick={
                      prenotazioneGiorno
                        ? () => setDialogo({ modo: 'modifica', prenotazione: prenotazioneGiorno })
                        : puoScrivere
                          ? () => setDialogo({ modo: 'crea', cameraId: camera.id, checkIn: giorni[0], checkOut: aggiungiGiorni(giorni[0], 1) })
                          : undefined
                    }
                  >
                    <TestataCardElenco
                      titolo={camera.nome}
                      sottotitolo={prenotazioneGiorno ? nomeOspite ?? (prenotazioneGiorno.numeroPrenotazione ? `#${prenotazioneGiorno.numeroPrenotazione}` : etichettaCanale) : undefined}
                      azioneDestra={
                        prenotazioneGiorno?.statoPrenotazione != null ? (
                          <Chip
                            size="small"
                            label={ETICHETTA_STATO[prenotazioneGiorno.statoPrenotazione]}
                            sx={{ bgcolor: COLORE_STATO[prenotazioneGiorno.statoPrenotazione], color: '#fff', fontWeight: 700 }}
                          />
                        ) : (
                          // Nessuna prenotazione in questo giorno: lo stato mostrato è quello di
                          // pulizia/manutenzione della camera (indipendente dal giorno), non lo stato
                          // di una prenotazione — stesso pallino colorato sempre visibile nella
                          // colonna camera della griglia desktop.
                          <Chip
                            size="small"
                            label={ETICHETTA_STATO_CAMERA[camera.stateRoom]}
                            sx={{ bgcolor: COLORE_STATO_CAMERA[camera.stateRoom], color: '#fff', fontWeight: 700 }}
                          />
                        )
                      }
                    />
                    {prenotazioneGiorno ? (
                      <RigaCardMeta
                        voci={[
                          { etichetta: 'Check-in', valore: FORMATTATORE_LABEL.format(new Date(prenotazioneGiorno.checkIn!)) },
                          { etichetta: 'Check-out', valore: FORMATTATORE_LABEL.format(new Date(prenotazioneGiorno.checkOut!)) },
                          { etichetta: 'Canale', valore: etichettaCanale ?? '—' },
                          { etichetta: 'Ospiti', valore: prenotazioneGiorno.numeroOspiti ?? '—' },
                        ]}
                      />
                    ) : (
                      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
                        {puoScrivere ? 'Libera — tocca per creare una prenotazione' : 'Libera'}
                      </Typography>
                    )}
                  </CardElenco>
                )
              })}
            </Box>
          ))}
        </Box>
      )}

      {!caricamento && formato === 'griglia' && gruppi.length > 0 && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, overflow: 'hidden', bgcolor: tokens.surface }}>
          {/* Intestazione giorni */}
          <Box sx={{ display: 'flex', position: 'sticky', top: 0, zIndex: 3, bgcolor: tokens.surface, borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
            <Box
              sx={{
                width: COL_CAMERA,
                flex: '0 0 auto',
                display: 'flex',
                alignItems: 'center',
                px: 1.75,
                borderRight: `1px solid ${tokens.surfaceBorder}`,
                position: 'sticky',
                left: 0,
                zIndex: 1,
                bgcolor: tokens.surface,
              }}
            >
              <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
                Camera
              </Typography>
            </Box>
            <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${giorniVisibili}, ${COL_GIORNO}px)` }}>
              {giorni.map((g) => {
                const weekend = g.getDay() === 0 || g.getDay() === 6
                const oggi = differenzaGiorni(g, new Date()) === 0
                return (
                  <Box
                    key={g.getTime()}
                    sx={{
                      height: 44,
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      justifyContent: 'center',
                      bgcolor: weekend ? tokens.paper : 'transparent',
                      borderLeft: `1px solid ${tokens.surfaceBorder}`,
                    }}
                  >
                    <Typography sx={{ fontSize: 10, fontWeight: 700, color: weekend ? tokens.orange700 : tokens.textTertiary, letterSpacing: '.04em' }}>
                      {NOMI_GIORNO[g.getDay()]}
                    </Typography>
                    <Typography
                      sx={{
                        fontFamily: fontMono,
                        fontSize: 13,
                        fontWeight: oggi ? 700 : 500,
                        color: oggi ? tokens.blue600 : weekend ? tokens.orange700 : tokens.textPrimary,
                      }}
                    >
                      {g.getDate()}
                    </Typography>
                  </Box>
                )
              })}
            </Box>
          </Box>

          {/* Righe camere, raggruppate per tipologia */}
          {gruppi.map((gruppo) => (
            <Box key={gruppo.tipologia}>
              <Box sx={{ display: 'flex', bgcolor: tokens.paper, borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
                <Box sx={{ position: 'sticky', left: 0, zIndex: 1, bgcolor: tokens.paper, width: '100%' }}>
                  <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textSecondary, textTransform: 'uppercase', letterSpacing: '.06em', px: 1.75, py: 0.5 }}>
                    {gruppo.tipologia}
                  </Typography>
                </Box>
              </Box>

              {gruppo.camere.map((camera) => (
                <RigaCamera
                  key={camera.id}
                  camera={camera}
                  giorni={giorni}
                  giorniVisibili={giorniVisibili}
                  inizioFinestra={inizioFinestra}
                  fineFinestra={fineFinestra}
                  prenotazioni={prenotazioniPerCamera.get(camera.id) ?? []}
                  onCellaVuota={
                    puoScrivere
                      ? (giorno) => setDialogo({ modo: 'crea', cameraId: camera.id, checkIn: giorno, checkOut: aggiungiGiorni(giorno, 1) })
                      : undefined
                  }
                  onPrenotazione={(p) => setDialogo({ modo: 'modifica', prenotazione: p })}
                  mappaColoriCanali={mappaColoriCanali}
                  agenzieStabili={agenzieStabili}
                />
              ))}
            </Box>
          ))}
        </Box>
      )}

      {!caricamento && formato === 'lista' && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {prenotazioniLista.length === 0 && <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>Nessuna prenotazione in questa vista.</Typography>}
          {prenotazioniLista.map((p) => {
            const nomeOspite = p.ospiteNome || p.ospiteCognome ? `${p.ospiteNome ?? ''} ${p.ospiteCognome ?? ''}`.trim() : null
            return (
              <CardElenco key={p.id} onClick={() => setDialogo({ modo: 'modifica', prenotazione: p })} coloreAccento={p.statoPrenotazione != null ? COLORE_STATO[p.statoPrenotazione] : undefined}>
                <TestataCardElenco
                  titolo={p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : p.cameraNome ?? '—'}
                  sottotitolo={nomeOspite ?? 'Ospite non ancora indicato'}
                  azioneDestra={
                    p.statoPrenotazione != null && (
                      <Chip size="small" label={ETICHETTA_STATO[p.statoPrenotazione]} sx={{ bgcolor: COLORE_STATO[p.statoPrenotazione], color: '#fff', fontWeight: 700 }} />
                    )
                  }
                />
                <RigaCardMeta
                  voci={[
                    { etichetta: 'Camera', valore: p.cameraNome ?? '—' },
                    { etichetta: 'Check-in', valore: p.checkIn ? FORMATTATORE_LABEL.format(new Date(p.checkIn)) : '—' },
                    { etichetta: 'Check-out', valore: p.checkOut ? FORMATTATORE_LABEL.format(new Date(p.checkOut)) : '—' },
                    { etichetta: 'Importo totale', valore: p.importoTotale != null ? formattatoreValuta.format(p.importoTotale) : '—' },
                    { etichetta: 'Importo pagato', valore: p.importoPagato != null ? formattatoreValuta.format(p.importoPagato) : '—' },
                  ]}
                />
              </CardElenco>
            )
          })}
        </Box>
      )}

      {!caricamento && formato === 'lista' && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Prenotazione</TableCell>
                <TableCell>Stato</TableCell>
                <TableCell>Ospite</TableCell>
                <TableCell>Camera</TableCell>
                <TableCell>Check-in</TableCell>
                <TableCell>Check-out</TableCell>
                <TableCell>Canale</TableCell>
                <TableCell align="right">Importo totale</TableCell>
                <TableCell align="right">Importo pagato</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {prenotazioniLista.length === 0 && (
                <TableRow>
                  <TableCell colSpan={9} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna prenotazione in questa vista.
                  </TableCell>
                </TableRow>
              )}
              {prenotazioniLista.map((p) => (
                <TableRow key={p.id} hover onClick={() => setDialogo({ modo: 'modifica', prenotazione: p })} sx={{ cursor: 'pointer' }}>
                  <TableCell sx={{ fontWeight: 700 }}>{p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : '—'}</TableCell>
                  <TableCell>
                    {p.statoPrenotazione != null && (
                      <Chip size="small" label={ETICHETTA_STATO[p.statoPrenotazione]} sx={{ bgcolor: COLORE_STATO[p.statoPrenotazione], color: '#fff', fontWeight: 700 }} />
                    )}
                  </TableCell>
                  <TableCell>{p.ospiteNome || p.ospiteCognome ? `${p.ospiteNome ?? ''} ${p.ospiteCognome ?? ''}`.trim() : '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.cameraNome ?? '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.checkIn ? FORMATTATORE_LABEL.format(new Date(p.checkIn)) : '—'}</TableCell>
                  <TableCell sx={{ fontFamily: fontMono }}>{p.checkOut ? FORMATTATORE_LABEL.format(new Date(p.checkOut)) : '—'}</TableCell>
                  <TableCell>{normalizzaAgenzia(p.agenzia)}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {p.importoTotale != null ? formattatoreValuta.format(p.importoTotale) : '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {p.importoPagato != null ? formattatoreValuta.format(p.importoPagato) : '—'}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo && strutturaId && (
        <PrenotazioneDialog
          strutturaId={strutturaId}
          stato={dialogo}
          camere={camere.data ?? []}
          canali={canali.data ?? []}
          tipologie={tipologie.data ?? []}
          onClose={() => setDialogo(null)}
        />
      )}
    </Box>
  )
}

function Legenda({
  agenzieDistinct,
  mappaColoriCanali,
  agenzieStabili,
}: {
  agenzieDistinct: string[]
  mappaColoriCanali: Map<string, string>
  agenzieStabili: string[]
}) {
  if (agenzieDistinct.length === 0) return null
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      {agenzieDistinct.map((etichetta) => (
        <Box key={etichetta} sx={{ display: 'flex', alignItems: 'center', gap: 0.625 }}>
          <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: coloreCanale(etichetta, mappaColoriCanali, agenzieStabili).colore }} />
          <Typography sx={{ fontSize: 11.5, color: tokens.textSecondary }}>{etichetta}</Typography>
        </Box>
      ))}
    </Box>
  )
}

interface RigaCameraProps {
  camera: CameraDto
  giorni: Date[]
  giorniVisibili: number
  inizioFinestra: Date
  fineFinestra: Date
  prenotazioni: PrenotazioneDto[]
  onCellaVuota?: (giorno: Date) => void
  onPrenotazione: (p: PrenotazioneDto) => void
  mappaColoriCanali: Map<string, string>
  agenzieStabili: string[]
}

function RigaCamera({ camera, giorni, giorniVisibili, inizioFinestra, fineFinestra, prenotazioni, onCellaVuota, onPrenotazione, mappaColoriCanali, agenzieStabili }: RigaCameraProps) {
  const barre = useMemo(() => {
    return prenotazioni
      .map((p) => {
        const ci = inizioGiornoLocale(new Date(p.checkIn!))
        const co = inizioGiornoLocale(new Date(p.checkOut!))
        const inizioClip = ci < inizioFinestra ? inizioFinestra : ci
        const fineClip = co > fineFinestra ? fineFinestra : co
        const startIdx = differenzaGiorni(inizioClip, inizioFinestra)
        const span = differenzaGiorni(fineClip, inizioClip)
        return { prenotazione: p, startIdx, span }
      })
      .filter((b) => b.span > 0 && b.startIdx < giorniVisibili && b.startIdx + b.span > 0)
  }, [prenotazioni, inizioFinestra, fineFinestra, giorniVisibili])

  return (
    <Box sx={{ display: 'flex', borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
      <Box
        sx={{
          width: COL_CAMERA,
          flex: '0 0 auto',
          display: 'flex',
          alignItems: 'center',
          gap: 0.875,
          px: 1.75,
          borderRight: `1px solid ${tokens.surfaceBorder}`,
          position: 'sticky',
          left: 0,
          zIndex: 1,
          bgcolor: tokens.surface,
        }}
      >
        <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: COLORE_STATO_CAMERA[camera.stateRoom], flex: '0 0 auto' }} />
        <Box sx={{ minWidth: 0 }}>
          <Typography noWrap sx={{ fontSize: 13, fontWeight: 700 }}>
            {camera.nome}
          </Typography>
          <Typography sx={{ fontSize: 10, color: tokens.textTertiary }}>{ETICHETTA_STATO_CAMERA[camera.stateRoom]}</Typography>
        </Box>
      </Box>

      <Box sx={{ position: 'relative', display: 'grid', gridTemplateColumns: `repeat(${giorniVisibili}, ${COL_GIORNO}px)`, height: RIGA_ALTEZZA }}>
        {giorni.map((g, idx) => {
          const weekend = g.getDay() === 0 || g.getDay() === 6
          return (
            <Box
              key={g.getTime()}
              onClick={() => onCellaVuota?.(g)}
              sx={{
                gridColumn: `${idx + 1} / span 1`,
                gridRow: 1,
                bgcolor: weekend ? tokens.paper : 'transparent',
                borderLeft: `1px solid ${tokens.surfaceBorder}`,
                cursor: onCellaVuota ? 'pointer' : 'default',
                '&:hover': onCellaVuota ? { bgcolor: '#F4F1EA' } : undefined,
              }}
            />
          )
        })}

        {barre.map(({ prenotazione, startIdx, span }) => {
          const { colore, etichetta } = coloreCanale(prenotazione.agenzia, mappaColoriCanali, agenzieStabili)
          const nomeOspite = prenotazione.ospiteNome || prenotazione.ospiteCognome ? `${prenotazione.ospiteNome ?? ''} ${prenotazione.ospiteCognome ?? ''}`.trim() : null
          const testoBarra = nomeOspite || (prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : etichetta)
          return (
            <Box
              key={prenotazione.id}
              onClick={(e) => {
                e.stopPropagation()
                onPrenotazione(prenotazione)
              }}
              title={`${etichetta} · ${prenotazione.numeroOspiti ?? '—'} ospiti`}
              sx={{
                gridColumn: `${startIdx + 1} / span ${span}`,
                gridRow: 1,
                alignSelf: 'center',
                height: 34,
                mx: '3px',
                borderRadius: '10px',
                bgcolor: colore,
                color: '#fff',
                display: 'flex',
                alignItems: 'center',
                px: 1.25,
                fontSize: 12,
                fontWeight: 700,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                cursor: 'pointer',
                boxShadow: '0 1px 2px rgba(20,25,34,0.18)',
                opacity: prenotazione.statoPrenotazione === StatoPrenotazione.Incompleta ? 0.72 : 1,
              }}
            >
              {testoBarra}
            </Box>
          )
        })}
      </Box>
    </Box>
  )
}
