import { useState } from 'react'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { useArriviInCorso, useArriviProssimi, type PrenotazioneDto } from '../api/prenotazioni'
import { useCamere } from '../api/camere'
import { useCanaliVendita } from '../api/canaliVendita'
import { useTipologie } from '../api/tipologie'
import { useRiepilogoCassa } from '../api/finanze'
import {
  useAlloggiatiWebConfig,
  useOsservatorioAppartamenti,
  usePayTouristConfig,
  usePayTouristStrutture,
  useWubookConfig,
} from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { aggiungiGiorni, inizioGiornoLocale } from '../lib/date'
import { PrenotazioneDialog, type StatoIniziale } from '../components/PrenotazioneDialog'
import { BottoneNuovo } from '../components/CardElenco'
import { KpiCard, KpiCardDoppia } from '../components/KpiCard'
import { usePuoScrivere } from '../permessi/usePuoScrivere'

/**
 * Le date arrivano dal backend come timestamp "locali alla struttura" ma serializzati con
 * suffisso UTC (vedi GestiSoftDbContext — sono ritaggate, non convertite). Per l'Italia
 * (sempre in anticipo su UTC) confrontare l'anno/mese/giorno letti in timezone locale del
 * browser resta corretto; non è una soluzione generale multi-fuso.
 */
function isOggi(iso: string | null): boolean {
  if (!iso) return false
  const d = new Date(iso)
  const oggi = new Date()
  return d.getFullYear() === oggi.getFullYear() && d.getMonth() === oggi.getMonth() && d.getDate() === oggi.getDate()
}

/** Oggi o prima — usato per le partenze: un check-out dimenticato non deve sparire dalla lista il giorno dopo, resta finché non viene fatto. */
function isOggiOPrima(iso: string | null): boolean {
  if (!iso) return false
  return inizioGiornoLocale(new Date(iso)) <= inizioGiornoLocale(new Date())
}

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

type EsitoIntegrazione = 'ok' | 'attesa' | 'errore' | 'non-configurato'

export function DashboardPage() {
  const { strutturaId, strutturaCorrente } = useStruttura()
  const [dialogo, setDialogo] = useState<StatoIniziale | null>(null)
  const puoScrivere = usePuoScrivere('reservationWrite')
  // Come in CamerePage: un solo permesso (SettingAgency/StatePoliceSettings) governa sia la
  // consultazione che la scrittura di questi dati — senza il permesso la query non parte nemmeno
  // (fallirebbe comunque con 403).
  const puoVedereCanali = usePuoScrivere('settingAgency')
  const puoVedereCredenzialiInvii = usePuoScrivere('statePoliceSettings')
  const puoVedereCamere = usePuoScrivere('settingRoomRead')
  const puoVedereFinanze = usePuoScrivere('financeRead')

  const arriviInCorso = useArriviInCorso(strutturaId)
  const arriviProssimi = useArriviProssimi(strutturaId)
  const camere = useCamere(puoVedereCamere ? strutturaId : null)
  const canali = useCanaliVendita(puoVedereCanali ? strutturaId : null)
  const tipologie = useTipologie(puoVedereCamere ? strutturaId : null)
  const cassa = useRiepilogoCassa(puoVedereFinanze ? strutturaId : null, new Date().getFullYear())

  // Come nelle voci di menu "Invii automatici" e in Impostazioni: un servizio non concesso dal
  // Super Admin a questa struttura non deve comparire affatto, non solo mostrare un pallino "non
  // configurato" — né vale la pena interrogarne lo stato.
  const wubookConcesso = strutturaCorrente?.wubookAbilitato ?? false
  const alloggiatiWebConcesso = strutturaCorrente?.alloggiatiWebAbilitato ?? false
  const osservatorioConcesso = strutturaCorrente?.osservatorioAbilitato ?? false
  const payTouristConcesso = strutturaCorrente?.payTouristAbilitato ?? false
  const nessunServizioConcesso = !wubookConcesso && !alloggiatiWebConcesso && !osservatorioConcesso && !payTouristConcesso

  const wubook = useWubookConfig(wubookConcesso && puoVedereCamere ? strutturaId : null)
  const alloggiatiWeb = useAlloggiatiWebConfig(alloggiatiWebConcesso && puoVedereCredenzialiInvii ? strutturaId : null)
  const osservatorio = useOsservatorioAppartamenti(osservatorioConcesso && puoVedereCredenzialiInvii ? strutturaId : null)
  const payTouristConfig = usePayTouristConfig(payTouristConcesso && puoVedereCredenzialiInvii ? strutturaId : null)
  const payTouristStrutture = usePayTouristStrutture(payTouristConcesso && puoVedereCredenzialiInvii ? strutturaId : null)

  const arriviOggi = (arriviProssimi.data ?? []).filter((p) => isOggi(p.checkIn))
  // "Oggi o prima", non solo "oggi": una partenza dimenticata resta InCorso e deve restare
  // visibile e cliccabile finché non si fa il check-out, non sparire dalla lista il giorno dopo.
  const partenzeDaFare = (arriviInCorso.data ?? []).filter((p) => isOggiOPrima(p.checkOut))
  const occupate = arriviInCorso.data?.length ?? null
  const totaleCamere = camere.data?.length ?? null

  const righeMovimenti: { prenotazione: PrenotazioneDto; tipo: 'check-in' | 'check-out' }[] = [
    ...arriviOggi.map((p) => ({ prenotazione: p, tipo: 'check-in' as const })),
    ...partenzeDaFare.map((p) => ({ prenotazione: p, tipo: 'check-out' as const })),
  ]

  const wubookStato: EsitoIntegrazione | undefined = wubook.data
    ? wubook.data.ultimoErrore
      ? 'errore'
      : wubook.data.credenzialiPronte
        ? 'ok'
        : 'non-configurato'
    : undefined

  const alloggiatiWebStato: EsitoIntegrazione | undefined = alloggiatiWeb.data
    ? !alloggiatiWeb.data.credenzialiConfigurate
      ? 'non-configurato'
      : alloggiatiWeb.data.ultimoErrore
        ? 'errore'
        : alloggiatiWeb.data.ultimoInvioAtUtc || alloggiatiWeb.data.ultimaVerificaOkAtUtc
          ? 'ok'
          : 'attesa'
    : undefined

  const osservatorioStato: EsitoIntegrazione | undefined = osservatorio.data
    ? osservatorio.data.length === 0
      ? 'non-configurato'
      : osservatorio.data.some((a) => a.ultimoErrore)
        ? 'errore'
        : osservatorio.data.every((a) => a.ultimoInvioAtUtc || a.ultimaVerificaOkAtUtc)
          ? 'ok'
          : 'attesa'
    : undefined

  const payTouristStato: EsitoIntegrazione | undefined =
    payTouristConfig.data && payTouristStrutture.data
      ? !payTouristConfig.data.tokenConfigurato || payTouristStrutture.data.length === 0
        ? 'non-configurato'
        : payTouristStrutture.data.some((s) => s.ultimoErrore)
          ? 'errore'
          : payTouristStrutture.data.every((s) => s.ultimoInvioAtUtc || s.ultimaVerificaOkAtUtc)
            ? 'ok'
            : 'attesa'
      : undefined

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary, textTransform: 'capitalize' }}>
          {formattatoreData.format(new Date())}
        </Typography>
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

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 2 }}>
        <KpiCardDoppia
          etichetta="Arrivi e partenze oggi"
          voci={[
            { valore: arriviProssimi.isLoading ? null : String(arriviOggi.length), etichetta: 'arrivi' },
            { valore: arriviInCorso.isLoading ? null : String(partenzeDaFare.length), etichetta: 'partenze' },
          ]}
        />
        <KpiCard
          etichetta="Occupazione"
          valore={
            arriviInCorso.isLoading || camere.isLoading
              ? null
              : occupate === null || totaleCamere === null
                ? '—'
                : `${occupate}/${totaleCamere}`
          }
          dettaglio={
            occupate !== null && totaleCamere !== null && totaleCamere > 0
              ? `${Math.round((occupate / totaleCamere) * 100)}%`
              : undefined
          }
        />
        <KpiCard
          etichetta="Saldo cassa (anno)"
          valore={cassa.isLoading ? null : cassa.data ? formattatoreValuta.format(cassa.data.saldo) : '—'}
          accento
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: nessunServizioConcesso ? '1fr' : '1.55fr 1fr', gap: 2.5, alignItems: 'start' }}>
        <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Arrivi di oggi e partenze</Typography>

          {(arriviInCorso.isLoading || arriviProssimi.isLoading) && <Skeleton variant="rounded" height={140} />}

          {!arriviInCorso.isLoading && !arriviProssimi.isLoading && righeMovimenti.length === 0 && (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessun arrivo previsto per oggi, nessuna partenza da fare.</Typography>
          )}

          {righeMovimenti.length > 0 && (
            <Box sx={{ display: 'flex', flexDirection: 'column' }}>
              <RigaMovimento intestazione />
              {righeMovimenti.map(({ prenotazione, tipo }) => (
                <RigaMovimento
                  key={`${prenotazione.id}-${tipo}`}
                  prenotazione={prenotazione}
                  tipo={tipo}
                  onClick={() => setDialogo({ modo: 'modifica', prenotazione })}
                />
              ))}
            </Box>
          )}
        </Box>

        {!nessunServizioConcesso && (
          <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Stato invii automatici</Typography>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
              {wubookConcesso && <RigaIntegrazione nome="OTA" stato={wubookStato} />}
              {alloggiatiWebConcesso && <RigaIntegrazione nome="Alloggiati Web" stato={alloggiatiWebStato} />}
              {osservatorioConcesso && <RigaIntegrazione nome="Osservatorio Turistico" stato={osservatorioStato} />}
              {payTouristConcesso && <RigaIntegrazione nome="PayTourist" stato={payTouristStato} />}
            </Box>
          </Box>
        )}
      </Box>

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

function RigaMovimento({
  intestazione,
  prenotazione,
  tipo,
  onClick,
}: {
  intestazione?: boolean
  prenotazione?: PrenotazioneDto
  tipo?: 'check-in' | 'check-out'
  onClick?: () => void
}) {
  if (intestazione) {
    return (
      <Box sx={{ display: 'grid', gridTemplateColumns: '1.4fr .8fr .8fr 1fr', p: '8px 4px', borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
        {['Prenotazione', 'Camera', 'Canale', 'Stato'].map((h) => (
          <Typography key={h} sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
            {h}
          </Typography>
        ))}
      </Box>
    )
  }

  if (!prenotazione || !tipo) return null

  // Una partenza il cui check-out previsto è già passato (dimenticata) merita un colore diverso dal
  // solito "Check-out" blu, per farla notare a colpo d'occhio invece di confonderla con quelle del giorno.
  const inRitardo = tipo === 'check-out' && !isOggi(prenotazione.checkOut)
  const badge =
    tipo === 'check-in'
      ? { colore: tokens.ok600, sfondo: tokens.ok100, testo: 'Check-in' }
      : inRitardo
        ? { colore: tokens.orange600, sfondo: tokens.orange100, testo: 'Check-out in ritardo' }
        : { colore: tokens.blue600, sfondo: tokens.blue100, testo: 'Check-out' }

  return (
    <Box
      onClick={onClick}
      sx={{
        display: 'grid',
        gridTemplateColumns: '1.4fr .8fr .8fr 1fr',
        p: '12px 4px',
        alignItems: 'center',
        borderBottom: `1px solid ${tokens.surfaceBorder}`,
        cursor: onClick ? 'pointer' : 'default',
        '&:hover': onClick ? { bgcolor: tokens.paper } : undefined,
      }}
    >
      <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>{prenotazione.numeroPrenotazione ?? '—'}</Typography>
      <Typography sx={{ fontFamily: fontMono, fontSize: 13, color: tokens.textSecondary }}>{prenotazione.cameraNome ?? '—'}</Typography>
      <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>{prenotazione.agenzia ?? '—'}</Typography>
      <Box
        sx={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 0.75,
          fontSize: 12,
          fontWeight: 700,
          color: badge.colore,
          bgcolor: badge.sfondo,
          borderRadius: 999,
          px: 1.25,
          py: 0.5,
          width: 'fit-content',
        }}
      >
        <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: badge.colore }} />
        {badge.testo}
      </Box>
    </Box>
  )
}

const testiStato: Record<EsitoIntegrazione, string> = {
  ok: 'attivo',
  attesa: 'in attesa',
  errore: 'errore',
  'non-configurato': 'non configurato',
}

const coloriStato: Record<EsitoIntegrazione, string> = {
  ok: tokens.ok600,
  attesa: tokens.wait600,
  errore: tokens.error600,
  'non-configurato': tokens.textTertiary,
}

function RigaIntegrazione({ nome, stato }: { nome: string; stato: EsitoIntegrazione | undefined }) {
  const inErrore = stato === 'errore'

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        p: '11px 12px',
        borderRadius: 2,
        border: `1px solid ${inErrore ? tokens.error600 : tokens.surfaceBorder}`,
        bgcolor: inErrore ? tokens.error100 : 'transparent',
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
        {stato === undefined ? (
          <Skeleton variant="circular" width={9} height={9} />
        ) : (
          <Box sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: coloriStato[stato] }} />
        )}
        <Typography sx={{ fontSize: 13.5, fontWeight: inErrore ? 700 : 600, color: inErrore ? tokens.error600 : tokens.textPrimary }}>{nome}</Typography>
      </Box>
      {stato !== undefined && (
        <Typography sx={{ fontSize: 11.5, fontWeight: inErrore ? 600 : 400, color: inErrore ? tokens.error600 : tokens.textTertiary }}>
          {testiStato[stato]}
        </Typography>
      )}
    </Box>
  )
}
