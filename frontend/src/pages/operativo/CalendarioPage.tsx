import { useEffect, useMemo, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import Skeleton from '@mui/material/Skeleton'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import SearchIcon from '@mui/icons-material/Search'
import TodayIcon from '@mui/icons-material/Today'
import { useStruttura } from '../../struttura/StrutturaContext'
import { StatoCamera, useCamere, type CameraDto } from '../../api/camere'
import { usePrenotazioniPeriodo, StatoPrenotazione, type PrenotazioneDto } from '../../api/prenotazioni'
import { useCanaliVendita } from '../../api/canaliVendita'
import { useTipologie } from '../../api/tipologie'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { aggiungiGiorni, differenzaGiorni, inizioGiornoLocale } from '../../lib/date'
import { useMobile } from '../../lib/useMobile'
import { PrenotazioneDialog, type StatoIniziale, ETICHETTA_STATO, COLORE_STATO } from '../../components/PrenotazioneDialog'
import { BottoneNuovo, CardElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

const GIORNI_VISIBILI_DEFAULT = 14
const GIORNI_VISIBILI_MIN = 7
const COL_CAMERA = 184
const COL_GIORNO = 74
const RIGA_ALTEZZA = 56

const NOMI_GIORNO = ['DOM', 'LUN', 'MAR', 'MER', 'GIO', 'VEN', 'SAB']
const FORMATTATORE_LABEL = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short', year: 'numeric' })

// Nessuna lista fissa di canali (Diretta/Booking.com/Airbnb...): i pallini colorati riflettono solo
// le agenzie che esistono davvero — la distinct dei valori Prenotazione.Agenzia effettivamente in
// uso nella finestra visibile del calendario, non un elenco statico né la tabella canali_vendita
// (che è solo un suggerimento testo, vedi PrenotazioneDialog).
const PALETTE_CANALI = [tokens.blue600, tokens.orange600, tokens.ok600, tokens.ink600, tokens.blue400, tokens.orange400, tokens.wait600, tokens.error600]

function normalizzaAgenzia(agenzia: string | null): string {
  const a = (agenzia ?? '').trim()
  return a === '' ? 'Diretta' : a
}

function coloreCanale(agenzia: string | null, agenzieDistinct: string[]): { colore: string; etichetta: string } {
  const etichetta = normalizzaAgenzia(agenzia)
  const idx = Math.max(0, agenzieDistinct.indexOf(etichetta))
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
  const [dialogo, setDialogo] = useState<StatoIniziale | null>(null)
  const [filtroTipologiaId, setFiltroTipologiaId] = useState('')
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

  const gruppi = useMemo(() => {
    if (!camere.data) return []
    const testoRicerca = ricerca.trim().toLowerCase()
    const filtrate = camere.data.filter((c) => {
      if (filtroTipologiaId && c.tipologiaId !== filtroTipologiaId) return false
      if (testoRicerca && !c.nome.toLowerCase().includes(testoRicerca)) return false
      return true
    })
    const mappa = new Map<string, CameraDto[]>()
    for (const c of filtrate) {
      const chiave = c.tipologiaNome ?? 'Senza tipologia'
      if (!mappa.has(chiave)) mappa.set(chiave, [])
      mappa.get(chiave)!.push(c)
    }
    return Array.from(mappa.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([tipologia, elenco]) => ({ tipologia, camere: elenco.sort((x, y) => x.nome.localeCompare(y.nome)) }))
  }, [camere.data, filtroTipologiaId, ricerca])

  const prenotazioniPerCamera = useMemo(() => {
    const mappa = new Map<string, PrenotazioneDto[]>()
    for (const p of prenotazioni.data ?? []) {
      if (!p.cameraId || !p.checkIn || !p.checkOut) continue
      if (!mappa.has(p.cameraId)) mappa.set(p.cameraId, [])
      mappa.get(p.cameraId)!.push(p)
    }
    return mappa
  }, [prenotazioni.data])

  const agenzieDistinct = useMemo(
    () => Array.from(new Set((prenotazioni.data ?? []).map((p) => normalizzaAgenzia(p.agenzia)))).sort(),
    [prenotazioni.data],
  )

  const caricamento = camere.isLoading || prenotazioni.isLoading

  return (
    <Box ref={contenitoreRef} sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, width: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, mobile ? -1 : -7))}>
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 14.5, px: 0.5, minWidth: mobile ? 130 : 190, textAlign: 'center' }}>
            {mobile ? FORMATTATORE_LABEL.format(giorni[0]) : `${FORMATTATORE_LABEL.format(giorni[0])} – ${FORMATTATORE_LABEL.format(giorni[giorni.length - 1])}`}
          </Typography>
          <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, mobile ? 1 : 7))}>
            <ChevronRightIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" onClick={() => setInizioFinestra(inizioGiornoLocale(new Date()))} title="Torna a oggi">
            <TodayIcon fontSize="small" />
          </IconButton>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Legenda agenzieDistinct={agenzieDistinct} />
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
          <TextField
            size="small"
            placeholder="Cerca camera..."
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

      {!caricamento && gruppi.length > 0 && mobile && (
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
                  ? coloreCanale(prenotazioneGiorno.agenzia, agenzieDistinct)
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

      {!caricamento && gruppi.length > 0 && !mobile && (
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
                  agenzieDistinct={agenzieDistinct}
                />
              ))}
            </Box>
          ))}
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

function Legenda({ agenzieDistinct }: { agenzieDistinct: string[] }) {
  if (agenzieDistinct.length === 0) return null
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      {agenzieDistinct.map((etichetta) => (
        <Box key={etichetta} sx={{ display: 'flex', alignItems: 'center', gap: 0.625 }}>
          <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: coloreCanale(etichetta, agenzieDistinct).colore }} />
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
  agenzieDistinct: string[]
}

function RigaCamera({ camera, giorni, giorniVisibili, inizioFinestra, fineFinestra, prenotazioni, onCellaVuota, onPrenotazione, agenzieDistinct }: RigaCameraProps) {
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
          const { colore, etichetta } = coloreCanale(prenotazione.agenzia, agenzieDistinct)
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
