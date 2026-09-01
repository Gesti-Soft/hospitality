import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
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

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

type EsitoIntegrazione = 'ok' | 'attesa' | 'errore' | 'non-configurato'

export function DashboardPage() {
  const { strutturaId, strutturaCorrente } = useStruttura()
  const [dialogo, setDialogo] = useState<StatoIniziale | null>(null)

  const arriviInCorso = useArriviInCorso(strutturaId)
  const arriviProssimi = useArriviProssimi(strutturaId)
  const camere = useCamere(strutturaId)
  const canali = useCanaliVendita(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const cassa = useRiepilogoCassa(strutturaId, new Date().getFullYear())

  // Come nelle voci di menu "Invii automatici" e in Impostazioni: un servizio non concesso dal
  // Super Admin a questa struttura non deve comparire affatto, non solo mostrare un pallino "non
  // configurato" — né vale la pena interrogarne lo stato.
  const wubookConcesso = strutturaCorrente?.wubookAbilitato ?? false
  const alloggiatiWebConcesso = strutturaCorrente?.alloggiatiWebAbilitato ?? false
  const osservatorioConcesso = strutturaCorrente?.osservatorioAbilitato ?? false
  const payTouristConcesso = strutturaCorrente?.payTouristAbilitato ?? false
  const nessunServizioConcesso = !wubookConcesso && !alloggiatiWebConcesso && !osservatorioConcesso && !payTouristConcesso

  const wubook = useWubookConfig(wubookConcesso ? strutturaId : null)
  const alloggiatiWeb = useAlloggiatiWebConfig(alloggiatiWebConcesso ? strutturaId : null)
  const osservatorio = useOsservatorioAppartamenti(osservatorioConcesso ? strutturaId : null)
  const payTouristConfig = usePayTouristConfig(payTouristConcesso ? strutturaId : null)
  const payTouristStrutture = usePayTouristStrutture(payTouristConcesso ? strutturaId : null)

  const arriviOggi = (arriviProssimi.data ?? []).filter((p) => isOggi(p.checkIn))
  const partenzeOggi = (arriviInCorso.data ?? []).filter((p) => isOggi(p.checkOut))
  const occupate = arriviInCorso.data?.length ?? null
  const totaleCamere = camere.data?.length ?? null

  const righeMovimenti: { prenotazione: PrenotazioneDto; tipo: 'check-in' | 'check-out' }[] = [
    ...arriviOggi.map((p) => ({ prenotazione: p, tipo: 'check-in' as const })),
    ...partenzeOggi.map((p) => ({ prenotazione: p, tipo: 'check-out' as const })),
  ]

  const wubookStato: EsitoIntegrazione | undefined = wubook.data
    ? !wubook.data.licenzaConfigurata
      ? 'non-configurato'
      : wubook.data.ultimoErrore
        ? 'errore'
        : wubook.data.cacheAggiornataAtUtc
          ? 'ok'
          : 'attesa'
    : undefined

  const alloggiatiWebStato: EsitoIntegrazione | undefined = alloggiatiWeb.data
    ? !alloggiatiWeb.data.credenzialiConfigurate
      ? 'non-configurato'
      : alloggiatiWeb.data.ultimoErrore
        ? 'errore'
        : alloggiatiWeb.data.ultimoInvioAtUtc
          ? 'ok'
          : 'attesa'
    : undefined

  const osservatorioStato: EsitoIntegrazione | undefined = osservatorio.data
    ? osservatorio.data.length === 0
      ? 'non-configurato'
      : osservatorio.data.some((a) => a.ultimoErrore)
        ? 'errore'
        : osservatorio.data.every((a) => a.ultimoInvioAtUtc)
          ? 'ok'
          : 'attesa'
    : undefined

  const payTouristStato: EsitoIntegrazione | undefined =
    payTouristConfig.data && payTouristStrutture.data
      ? !payTouristConfig.data.tokenConfigurato || payTouristStrutture.data.length === 0
        ? 'non-configurato'
        : payTouristStrutture.data.some((s) => s.ultimoErrore)
          ? 'errore'
          : payTouristStrutture.data.every((s) => s.ultimoInvioAtUtc)
            ? 'ok'
            : 'attesa'
      : undefined

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary, textTransform: 'capitalize' }}>
          {formattatoreData.format(new Date())}
        </Typography>
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

      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 2 }}>
        <KpiCardDoppia
          etichetta="Arrivi e partenze oggi"
          voci={[
            { valore: arriviProssimi.isLoading ? null : String(arriviOggi.length), etichetta: 'arrivi' },
            { valore: arriviInCorso.isLoading ? null : String(partenzeOggi.length), etichetta: 'partenze' },
          ]}
        />
        <KpiCard
          etichetta="Occupazione"
          valore={occupate === null || totaleCamere === null ? null : `${occupate}/${totaleCamere}`}
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
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Arrivi e partenze di oggi</Typography>

          {(arriviInCorso.isLoading || arriviProssimi.isLoading) && <Skeleton variant="rounded" height={140} />}

          {!arriviInCorso.isLoading && !arriviProssimi.isLoading && righeMovimenti.length === 0 && (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessun arrivo o partenza previsti per oggi.</Typography>
          )}

          {righeMovimenti.length > 0 && (
            <Box sx={{ display: 'flex', flexDirection: 'column' }}>
              <RigaMovimento intestazione />
              {righeMovimenti.map(({ prenotazione, tipo }) => (
                <RigaMovimento key={`${prenotazione.id}-${tipo}`} prenotazione={prenotazione} tipo={tipo} />
              ))}
            </Box>
          )}
        </Box>

        {!nessunServizioConcesso && (
          <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Stato invii automatici</Typography>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
              {wubookConcesso && <RigaIntegrazione nome="Wubook" stato={wubookStato} />}
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

function KpiCard({ etichetta, valore, dettaglio, accento }: { etichetta: string; valore: string | null; dettaglio?: string; accento?: boolean }) {
  return (
    <Box
      sx={{
        bgcolor: tokens.surface,
        border: `1px solid ${accento ? tokens.orange600 : tokens.surfaceBorder}`,
        borderWidth: accento ? 1.5 : 1,
        borderRadius: 2,
        p: '20px 22px',
      }}
    >
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: accento ? tokens.orange700 : tokens.textSecondary }}>{etichetta}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, mt: 1 }}>
        {valore === null ? (
          <Skeleton width={60} height={34} />
        ) : (
          <Typography sx={{ fontFamily: fontMono, fontSize: 28, fontWeight: 600 }}>{valore}</Typography>
        )}
        {dettaglio && valore !== null && <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>{dettaglio}</Typography>}
      </Box>
    </Box>
  )
}

function KpiCardDoppia({ etichetta, voci }: { etichetta: string; voci: { valore: string | null; etichetta: string }[] }) {
  return (
    <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: '20px 22px' }}>
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: tokens.textSecondary }}>{etichetta}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 3, mt: 1 }}>
        {voci.map((v) => (
          <Box key={v.etichetta} sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
            {v.valore === null ? (
              <Skeleton width={40} height={34} />
            ) : (
              <Typography sx={{ fontFamily: fontMono, fontSize: 28, fontWeight: 600 }}>{v.valore}</Typography>
            )}
            {v.valore !== null && <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>{v.etichetta}</Typography>}
          </Box>
        ))}
      </Box>
    </Box>
  )
}

function RigaMovimento({ intestazione, prenotazione, tipo }: { intestazione?: boolean; prenotazione?: PrenotazioneDto; tipo?: 'check-in' | 'check-out' }) {
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

  const badge =
    tipo === 'check-in'
      ? { colore: tokens.ok600, sfondo: tokens.ok100, testo: 'Check-in' }
      : { colore: tokens.blue600, sfondo: tokens.blue100, testo: 'Check-out' }

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1.4fr .8fr .8fr 1fr', p: '12px 4px', alignItems: 'center', borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
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
