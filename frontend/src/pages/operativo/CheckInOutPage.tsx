import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/AddShoppingCartOutlined'
import LoginIcon from '@mui/icons-material/LoginOutlined'
import LogoutIcon from '@mui/icons-material/LogoutOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useArriviInCorso, useArriviProssimi, useCheckIn, type PrenotazioneDto } from '../../api/prenotazioni'
import { AddebitaServizioDialog } from '../../components/AddebitaServizioDialog'
import { CheckOutDialog } from '../../components/CheckOutDialog'
import { useServizi } from '../../api/servizi'
import { ConfermaSchedinaSoggiornoBreve, isSoggiornoBreve } from '../../components/ConfermaSchedinaSoggiornoBreve'
import { useCamere } from '../../api/camere'
import { useTipologie } from '../../api/tipologie'
import { ApiError } from '../../api/client'
import { useToast } from '../../toast/ToastContext'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { isOggi, isOggiOPrima } from '../../lib/date'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { OspiteDialog } from '../../components/OspiteDialog'
import { ProponiFatturaDopoCheckIn } from '../../components/ProponiFatturaDopoCheckIn'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

/** Pagina dedicata per fare check-in/check-out in un clic, senza passare dal dialog di dettaglio prenotazione — stessi endpoint/permesso (checkInOut) già usati lì; senza quel permesso la pagina è in sola lettura. */
export function CheckInOutPage() {
  const { strutturaId } = useStruttura()
  const arriviProssimi = useArriviProssimi(strutturaId)
  const arriviInCorso = useArriviInCorso(strutturaId)
  const camere = useCamere(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const checkIn = useCheckIn(strutturaId)
  const [checkOutDaConfermare, setCheckOutDaConfermare] = useState<PrenotazioneDto | null>(null)
  const [daAddebitare, setDaAddebitare] = useState<PrenotazioneDto | null>(null)
  // L'addebito rapido c'è solo se si possono modificare le prenotazioni e la struttura vende qualcosa.
  const puoAddebitare = usePuoScrivere('reservationWrite')
  const servizi = useServizi(puoAddebitare ? strutturaId : null)
  const addebitoDisponibile = puoAddebitare && (servizi.data ?? []).some((s) => s.attivo)
  // Il check-in si apre sulla scheda ospiti da compilare (Nome/Cognome veri, non un testo libero) —
  // ma il check-in vero e proprio scatta solo al salvataggio della scheda, mai chiudendo il dialog
  // senza salvare (altrimenti risulterebbe fatto anche annullando).
  const [ospiteDaCompilare, setOspiteDaCompilare] = useState<PrenotazioneDto | null>(null)
  // Soggiorno sotto le 24 ore appena messo in corso: la schedina ha 6 ore di tempo, non 24, quindi
  // si chiede subito se trasmetterla invece di lasciarla al batch giornaliero (vedi il dialog).
  const [schedinaBreveDaInviare, setSchedinaBreveDaInviare] = useState<PrenotazioneDto | null>(null)
  // Dopo il check-in si propone la fattura: da quel momento il soggiorno è fatturabile. Se prima c'è
  // la domanda sulla schedina del soggiorno breve, la fattura aspetta che quella sia chiusa.
  // Senza "Esegui check-in/out" la pagina è in sola lettura: chi prepara le camere vede arrivi e
  // partenze del giorno, non li registra.
  const puoFareCheckInOut = usePuoScrivere('checkInOut')
  const puoFatturare = usePuoScrivere('financeWrite')
  const [fatturaDaProporre, setFatturaDaProporre] = useState<PrenotazioneDto | null>(null)
  const [fatturaInAttesa, setFatturaInAttesa] = useState<PrenotazioneDto | null>(null)
  const toast = useToast()

  const arriviOggi = (arriviProssimi.data ?? []).filter((p) => isOggi(p.checkIn))
  // "Oggi o prima": una partenza dimenticata resta In corso e deve restare visibile finché non si fa il check-out.
  const partenzeDaFare = (arriviInCorso.data ?? []).filter((p) => isOggiOPrima(p.checkOut))
  // Chi è in struttura e non parte oggi: è a loro che si addebitano SPA, bar, escursioni.
  const inStruttura = (arriviInCorso.data ?? []).filter((p) => !isOggiOPrima(p.checkOut))

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function confermaCheckIn(p: PrenotazioneDto) {
    checkIn.mutate(p.id, {
      onError: gestisciErrore,
      onSuccess: (aggiornata) => {
        const daFatturare = puoFatturare ? aggiornata : null
        if (isSoggiornoBreve(p.checkIn, p.checkOut)) {
          setSchedinaBreveDaInviare(p)
          setFatturaInAttesa(daFatturare)
          return
        }
        setFatturaDaProporre(daFatturare)
      },
    })
  }

  // La cauzione si chiede solo se è davvero prevista su questa prenotazione (tipologia della camera
  // con un importo configurato E toggle "Cauzione" attivo). Il dialogo di check-out si apre sempre:
  // mostra quanto resta da saldare, che si deve vedere prima di lasciar andare l'ospite.
  function cauzionePrevista(p: PrenotazioneDto) {
    const tipologiaId = camere.data?.find((c) => c.id === p.cameraId)?.tipologiaId
    const cauzione = tipologie.data?.find((t) => t.id === tipologiaId)?.cauzione ?? 0
    return p.cauzioneAttiva && cauzione > 0
  }

  const caricamento = arriviProssimi.isLoading || arriviInCorso.isLoading || camere.isLoading || tipologie.isLoading

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Arrivi di oggi</Typography>
        {caricamento && <Skeleton variant="rounded" height={140} />}
        {!caricamento && arriviOggi.length === 0 && <MessaggioVuotoElenco messaggio="Nessun arrivo previsto per oggi." />}
        {!caricamento && arriviOggi.length > 0 && (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
            {arriviOggi.map((p) => (
              <CardPrenotazione key={p.id} prenotazione={p} tipo="check-in" inCorso={checkIn.isPending} onAzione={puoFareCheckInOut ? () => setOspiteDaCompilare(p) : undefined} />
            ))}
          </Box>
        )}
      </Box>

      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>Partenze</Typography>
        {caricamento && <Skeleton variant="rounded" height={140} />}
        {!caricamento && partenzeDaFare.length === 0 && <MessaggioVuotoElenco messaggio="Nessuna partenza da fare." />}
        {!caricamento && partenzeDaFare.length > 0 && (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
            {partenzeDaFare.map((p) => (
              <CardPrenotazione
                key={p.id}
                prenotazione={p}
                tipo="check-out"
                inCorso={false}
                onAzione={puoFareCheckInOut ? () => setCheckOutDaConfermare(p) : undefined}
                onAddebita={addebitoDisponibile ? () => setDaAddebitare(p) : undefined}
              />
            ))}
          </Box>
        )}
      </Box>

      {/* Solo se c'è qualcosa da addebitare: per chi non vende extra sarebbe un elenco in più. */}
      {addebitoDisponibile && (
        <Box>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>In struttura</Typography>
          {caricamento && <Skeleton variant="rounded" height={140} />}
          {!caricamento && inStruttura.length === 0 && <MessaggioVuotoElenco messaggio="Nessun altro ospite in struttura." />}
          {!caricamento && inStruttura.length > 0 && (
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
              {inStruttura.map((p) => (
                <CardPrenotazione key={p.id} prenotazione={p} tipo="in-struttura" inCorso={false} onAddebita={() => setDaAddebitare(p)} />
              ))}
            </Box>
          )}
        </Box>
      )}

      {checkOutDaConfermare && strutturaId && (
        <CheckOutDialog
          strutturaId={strutturaId}
          prenotazione={checkOutDaConfermare}
          cauzionePrevista={cauzionePrevista(checkOutDaConfermare)}
          onChiudi={() => setCheckOutDaConfermare(null)}
          onCompletato={() => setCheckOutDaConfermare(null)}
        />
      )}

      {daAddebitare && strutturaId && (
        <AddebitaServizioDialog strutturaId={strutturaId} prenotazione={daAddebitare} onChiudi={() => setDaAddebitare(null)} />
      )}

      {schedinaBreveDaInviare && (
        <ConfermaSchedinaSoggiornoBreve
          strutturaId={strutturaId}
          prenotazioneId={schedinaBreveDaInviare.id}
          riferimento={[schedinaBreveDaInviare.ospiteCognome, schedinaBreveDaInviare.ospiteNome].filter(Boolean).join(' ') || schedinaBreveDaInviare.numeroPrenotazione}
          onChiudi={() => {
            setSchedinaBreveDaInviare(null)
            setFatturaDaProporre(fatturaInAttesa)
            setFatturaInAttesa(null)
          }}
        />
      )}

      {fatturaDaProporre && strutturaId && (
        <ProponiFatturaDopoCheckIn strutturaId={strutturaId} prenotazione={fatturaDaProporre} onChiudi={() => setFatturaDaProporre(null)} />
      )}

      {ospiteDaCompilare && strutturaId && (
        <OspiteDialog
          strutturaId={strutturaId}
          prenotazione={ospiteDaCompilare}
          onClose={() => setOspiteDaCompilare(null)}
          dopoSalvataggio={() => confermaCheckIn(ospiteDaCompilare)}
        />
      )}
    </Box>
  )
}

function CardPrenotazione({
  prenotazione,
  tipo,
  inCorso,
  onAzione,
  onAddebita,
}: {
  prenotazione: PrenotazioneDto
  tipo: 'check-in' | 'check-out' | 'in-struttura'
  inCorso: boolean
  /** Assente = sola lettura (chi prepara le camere): niente pulsante e niente canale, che l'Api non gli manda. */
  onAzione?: () => void
  /** Addebito di un servizio extra sul conto (SPA, minibar…): solo a chi modifica le prenotazioni. */
  onAddebita?: () => void
}) {
  const ospite = [prenotazione.ospiteNome, prenotazione.ospiteCognome].filter(Boolean).join(' ')
  const inRitardo = tipo === 'check-out' && !isOggi(prenotazione.checkOut)

  return (
    <CardElenco coloreAccento={tipo === 'check-in' ? tokens.ok600 : inRitardo ? tokens.orange600 : tipo === 'in-struttura' ? tokens.surfaceBorder : tokens.blue600}>
      <TestataCardElenco
        titolo={ospite || 'Ospite da registrare'}
        sottotitolo={prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : undefined}
        azioneDestra={inRitardo ? <Chip size="small" label="In ritardo" sx={{ bgcolor: tokens.orange600, color: '#fff', fontWeight: 700 }} /> : undefined}
      />
      <RigaCardMeta
        voci={[
          { etichetta: 'Camera', valore: <span style={{ fontFamily: fontMono }}>{prenotazione.cameraNome ?? '—'}</span> },
          ...(onAzione || onAddebita ? [{ etichetta: 'Canale', valore: prenotazione.agenzia ?? 'Diretta' }] : []),
          { etichetta: 'Check-in', valore: prenotazione.checkIn ? formattatoreData.format(new Date(prenotazione.checkIn)) : '—' },
          { etichetta: 'Check-out', valore: prenotazione.checkOut ? formattatoreData.format(new Date(prenotazione.checkOut)) : '—' },
          // Quante persone si presentano al banco: serve per preparare la camera senza dover aprire
          // la prenotazione.
          { etichetta: 'Ospiti', valore: prenotazione.numeroOspiti ?? '—' },
        ]}
      />
      {(onAzione || onAddebita) && (
        <AzioniCardElenco>
          {onAddebita && (
            <Button fullWidth={!onAzione} variant="outlined" startIcon={<AddIcon />} onClick={onAddebita} disabled={inCorso}>
              Addebita
            </Button>
          )}
          {onAzione && (
            <Button
              fullWidth
              variant="contained"
              color="primary"
              startIcon={tipo === 'check-in' ? <LoginIcon /> : <LogoutIcon />}
              onClick={onAzione}
              disabled={inCorso}
            >
              {tipo === 'check-in' ? 'Check-in' : 'Check-out'}
            </Button>
          )}
        </AzioniCardElenco>
      )}
    </CardElenco>
  )
}
