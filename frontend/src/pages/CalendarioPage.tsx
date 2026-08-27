import { useMemo, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Typography from '@mui/material/Typography'
import Skeleton from '@mui/material/Skeleton'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import TodayIcon from '@mui/icons-material/Today'
import { useStruttura } from '../struttura/StrutturaContext'
import { StatoCamera, useCamere, type CameraDto } from '../api/camere'
import { usePrenotazioniPeriodo, StatoPrenotazione, type PrenotazioneDto } from '../api/prenotazioni'
import { useCanaliVendita } from '../api/canaliVendita'
import { fontDisplay, fontMono, tokens } from '../theme'
import { aggiungiGiorni, differenzaGiorni, inizioGiornoLocale } from '../lib/date'
import { PrenotazioneDialog, type StatoIniziale } from '../components/PrenotazioneDialog'

const GIORNI_VISIBILI = 14
const COL_CAMERA = 184
const COL_GIORNO = 74
const RIGA_ALTEZZA = 56

const CANALE_AIRBNB = '#3E8E82'
const CANALE_EXPEDIA = '#6B7A8F'

const NOMI_GIORNO = ['DOM', 'LUN', 'MAR', 'MER', 'GIO', 'VEN', 'SAB']
const FORMATTATORE_LABEL = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short', year: 'numeric' })

function coloreCanale(agenzia: string | null): { colore: string; etichetta: string } {
  const a = (agenzia ?? '').trim().toLowerCase()
  if (a.includes('booking')) return { colore: tokens.blue600, etichetta: 'Booking.com' }
  if (a.includes('airbnb')) return { colore: CANALE_AIRBNB, etichetta: 'Airbnb' }
  if (a.includes('expedia')) return { colore: CANALE_EXPEDIA, etichetta: 'Expedia' }
  if (a === '' || a === 'diretta') return { colore: tokens.orange600, etichetta: 'Diretta' }
  return { colore: tokens.ink600, etichetta: agenzia ?? 'Altro' }
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
  const { strutturaId } = useStruttura()
  const [inizioFinestra, setInizioFinestra] = useState(() => inizioGiornoLocale(new Date()))
  const [dialogo, setDialogo] = useState<StatoIniziale | null>(null)

  const fineFinestra = useMemo(() => aggiungiGiorni(inizioFinestra, GIORNI_VISIBILI), [inizioFinestra])
  const giorni = useMemo(
    () => Array.from({ length: GIORNI_VISIBILI }, (_, i) => aggiungiGiorni(inizioFinestra, i)),
    [inizioFinestra],
  )

  const camere = useCamere(strutturaId)
  const prenotazioni = usePrenotazioniPeriodo(strutturaId, inizioFinestra, fineFinestra)
  const canali = useCanaliVendita(strutturaId)

  const gruppi = useMemo(() => {
    if (!camere.data) return []
    const mappa = new Map<string, CameraDto[]>()
    for (const c of camere.data) {
      const chiave = c.tipologiaNome ?? 'Senza tipologia'
      if (!mappa.has(chiave)) mappa.set(chiave, [])
      mappa.get(chiave)!.push(c)
    }
    return Array.from(mappa.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([tipologia, elenco]) => ({ tipologia, camere: elenco.sort((x, y) => x.nome.localeCompare(y.nome)) }))
  }, [camere.data])

  const prenotazioniPerCamera = useMemo(() => {
    const mappa = new Map<string, PrenotazioneDto[]>()
    for (const p of prenotazioni.data ?? []) {
      if (!p.cameraId || !p.checkIn || !p.checkOut) continue
      if (!mappa.has(p.cameraId)) mappa.set(p.cameraId, [])
      mappa.get(p.cameraId)!.push(p)
    }
    return mappa
  }, [prenotazioni.data])

  const caricamento = camere.isLoading || prenotazioni.isLoading

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, -7))}>
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 14.5, px: 0.5, minWidth: 190, textAlign: 'center' }}>
            {FORMATTATORE_LABEL.format(giorni[0])} – {FORMATTATORE_LABEL.format(giorni[giorni.length - 1])}
          </Typography>
          <IconButton size="small" onClick={() => setInizioFinestra((d) => aggiungiGiorni(d, 7))}>
            <ChevronRightIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" onClick={() => setInizioFinestra(inizioGiornoLocale(new Date()))} title="Torna a oggi">
            <TodayIcon fontSize="small" />
          </IconButton>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Legenda />
          <Button
            variant="contained"
            color="secondary"
            size="medium"
            disabled={!strutturaId || !camere.data || camere.data.length === 0}
            onClick={() =>
              setDialogo({
                modo: 'crea',
                cameraId: null,
                checkIn: inizioGiornoLocale(new Date()),
                checkOut: aggiungiGiorni(new Date(), 1),
              })
            }
          >
            + Nuova prenotazione
          </Button>
        </Box>
      </Box>

      {caricamento && <Skeleton variant="rounded" height={420} />}

      {!caricamento && camere.data && camere.data.length === 0 && (
        <Box sx={{ border: `1px dashed ${tokens.surfaceBorder}`, borderRadius: 2, p: 6, textAlign: 'center', color: tokens.textSecondary }}>
          Nessuna camera configurata per questa struttura. Aggiungila dalla sezione Camere prima di poter creare prenotazioni.
        </Box>
      )}

      {!caricamento && gruppi.length > 0 && (
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
            <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${GIORNI_VISIBILI}, ${COL_GIORNO}px)` }}>
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
                  inizioFinestra={inizioFinestra}
                  fineFinestra={fineFinestra}
                  prenotazioni={prenotazioniPerCamera.get(camera.id) ?? []}
                  onCellaVuota={(giorno) =>
                    setDialogo({ modo: 'crea', cameraId: camera.id, checkIn: giorno, checkOut: aggiungiGiorni(giorno, 1) })
                  }
                  onPrenotazione={(p) => setDialogo({ modo: 'modifica', prenotazione: p })}
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
          onClose={() => setDialogo(null)}
        />
      )}
    </Box>
  )
}

function Legenda() {
  const voci = [
    { etichetta: 'Diretta', colore: tokens.orange600 },
    { etichetta: 'Booking.com', colore: tokens.blue600 },
    { etichetta: 'Airbnb', colore: CANALE_AIRBNB },
    { etichetta: 'Expedia', colore: CANALE_EXPEDIA },
  ]
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      {voci.map((v) => (
        <Box key={v.etichetta} sx={{ display: 'flex', alignItems: 'center', gap: 0.625 }}>
          <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: v.colore }} />
          <Typography sx={{ fontSize: 11.5, color: tokens.textSecondary }}>{v.etichetta}</Typography>
        </Box>
      ))}
    </Box>
  )
}

interface RigaCameraProps {
  camera: CameraDto
  giorni: Date[]
  inizioFinestra: Date
  fineFinestra: Date
  prenotazioni: PrenotazioneDto[]
  onCellaVuota: (giorno: Date) => void
  onPrenotazione: (p: PrenotazioneDto) => void
}

function RigaCamera({ camera, giorni, inizioFinestra, fineFinestra, prenotazioni, onCellaVuota, onPrenotazione }: RigaCameraProps) {
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
      .filter((b) => b.span > 0 && b.startIdx < GIORNI_VISIBILI && b.startIdx + b.span > 0)
  }, [prenotazioni, inizioFinestra, fineFinestra])

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

      <Box sx={{ position: 'relative', display: 'grid', gridTemplateColumns: `repeat(${GIORNI_VISIBILI}, ${COL_GIORNO}px)`, height: RIGA_ALTEZZA }}>
        {giorni.map((g, idx) => {
          const weekend = g.getDay() === 0 || g.getDay() === 6
          return (
            <Box
              key={g.getTime()}
              onClick={() => onCellaVuota(g)}
              sx={{
                gridColumn: `${idx + 1} / span 1`,
                gridRow: 1,
                bgcolor: weekend ? tokens.paper : 'transparent',
                borderLeft: `1px solid ${tokens.surfaceBorder}`,
                cursor: 'pointer',
                '&:hover': { bgcolor: '#F4F1EA' },
              }}
            />
          )
        })}

        {barre.map(({ prenotazione, startIdx, span }) => {
          const { colore, etichetta } = coloreCanale(prenotazione.agenzia)
          const nomeOspite = prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : etichetta
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
              {nomeOspite}
            </Box>
          )
        })}
      </Box>
    </Box>
  )
}
