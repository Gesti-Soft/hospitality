import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import LoginIcon from '@mui/icons-material/LoginOutlined'
import LogoutIcon from '@mui/icons-material/LogoutOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useArriviInCorso, useArriviProssimi, useCheckIn, useCheckOut, type PrenotazioneDto } from '../../api/prenotazioni'
import { useCamere } from '../../api/camere'
import { useTipologie } from '../../api/tipologie'
import { ApiError } from '../../api/client'
import { useToast } from '../../toast/ToastContext'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { isOggi, isOggiOPrima } from '../../lib/date'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { OspiteDialog } from '../../components/OspiteDialog'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

/** Pagina dedicata per fare check-in/check-out in un clic, senza passare dal dialog di dettaglio prenotazione — stessi endpoint/permesso (roomStatusUpdate) già usati lì. */
export function CheckInOutPage() {
  const { strutturaId } = useStruttura()
  const arriviProssimi = useArriviProssimi(strutturaId)
  const arriviInCorso = useArriviInCorso(strutturaId)
  const camere = useCamere(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const checkIn = useCheckIn(strutturaId)
  const checkOut = useCheckOut(strutturaId)
  const [checkOutDaConfermare, setCheckOutDaConfermare] = useState<PrenotazioneDto | null>(null)
  // Il check-in si apre sulla scheda ospiti da compilare (Nome/Cognome veri, non un testo libero) —
  // ma il check-in vero e proprio scatta solo al salvataggio della scheda, mai chiudendo il dialog
  // senza salvare (altrimenti risulterebbe fatto anche annullando).
  const [ospiteDaCompilare, setOspiteDaCompilare] = useState<PrenotazioneDto | null>(null)
  const toast = useToast()

  const arriviOggi = (arriviProssimi.data ?? []).filter((p) => isOggi(p.checkIn))
  // "Oggi o prima": una partenza dimenticata resta In corso e deve restare visibile finché non si fa il check-out.
  const partenzeDaFare = (arriviInCorso.data ?? []).filter((p) => isOggiOPrima(p.checkOut))

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function confermaCheckIn(p: PrenotazioneDto) {
    checkIn.mutate(p.id, { onError: gestisciErrore })
  }

  // La cauzione va chiesta solo se è davvero prevista su questa prenotazione (tipologia della
  // camera con un importo configurato E toggle "Cauzione" attivo) — altrimenti il check-out parte
  // subito in un clic, senza far comparire un dialog per nulla.
  function apriCheckOut(p: PrenotazioneDto) {
    const tipologiaId = camere.data?.find((c) => c.id === p.cameraId)?.tipologiaId
    const cauzione = tipologie.data?.find((t) => t.id === tipologiaId)?.cauzione ?? 0
    if (!p.cauzioneAttiva || cauzione <= 0) {
      eseguiCheckOut(p, true, null)
      return
    }
    setCheckOutDaConfermare(p)
  }

  function eseguiCheckOut(p: PrenotazioneDto, restituisciCauzione: boolean, importoCauzioneTrattenuta: number | null) {
    checkOut.mutate(
      { prenotazioneId: p.id, restituisciCauzione, importoCauzioneTrattenuta },
      {
        onSuccess: () => {
          toast.successo(`Check-out effettuato${p.numeroPrenotazione ? ` — #${p.numeroPrenotazione}` : ''}.`)
          setCheckOutDaConfermare(null)
        },
        onError: gestisciErrore,
      },
    )
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
              <CardPrenotazione key={p.id} prenotazione={p} tipo="check-in" inCorso={checkIn.isPending} onAzione={() => setOspiteDaCompilare(p)} />
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
              <CardPrenotazione key={p.id} prenotazione={p} tipo="check-out" inCorso={checkOut.isPending} onAzione={() => apriCheckOut(p)} />
            ))}
          </Box>
        )}
      </Box>

      {checkOutDaConfermare && (
        <CheckOutDialog
          prenotazione={checkOutDaConfermare}
          inCorso={checkOut.isPending}
          onConferma={(restituisci, importo) => eseguiCheckOut(checkOutDaConfermare, restituisci, importo)}
          onAnnulla={() => setCheckOutDaConfermare(null)}
        />
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
}: {
  prenotazione: PrenotazioneDto
  tipo: 'check-in' | 'check-out'
  inCorso: boolean
  onAzione: () => void
}) {
  const ospite = [prenotazione.ospiteNome, prenotazione.ospiteCognome].filter(Boolean).join(' ')
  const inRitardo = tipo === 'check-out' && !isOggi(prenotazione.checkOut)

  return (
    <CardElenco coloreAccento={tipo === 'check-in' ? tokens.ok600 : inRitardo ? tokens.orange600 : tokens.blue600}>
      <TestataCardElenco
        titolo={ospite || 'Ospite da registrare'}
        sottotitolo={prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : undefined}
        azioneDestra={inRitardo ? <Chip size="small" label="In ritardo" sx={{ bgcolor: tokens.orange600, color: '#fff', fontWeight: 700 }} /> : undefined}
      />
      <RigaCardMeta
        voci={[
          { etichetta: 'Camera', valore: <span style={{ fontFamily: fontMono }}>{prenotazione.cameraNome ?? '—'}</span> },
          { etichetta: 'Canale', valore: prenotazione.agenzia ?? 'Diretta' },
          { etichetta: 'Check-in', valore: prenotazione.checkIn ? formattatoreData.format(new Date(prenotazione.checkIn)) : '—' },
          { etichetta: 'Check-out', valore: prenotazione.checkOut ? formattatoreData.format(new Date(prenotazione.checkOut)) : '—' },
        ]}
      />
      <AzioniCardElenco>
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
      </AzioniCardElenco>
    </CardElenco>
  )
}

/** Stessa scelta cauzione già presente nel dialog di dettaglio prenotazione, isolata qui per il check-out in un clic da questa pagina. */
function CheckOutDialog({
  prenotazione,
  inCorso,
  onConferma,
  onAnnulla,
}: {
  prenotazione: PrenotazioneDto
  inCorso: boolean
  onConferma: (restituisciCauzione: boolean, importoCauzioneTrattenuta: number | null) => void
  onAnnulla: () => void
}) {
  const [restituisciCauzione, setRestituisciCauzione] = useState(true)
  const [importoCauzioneTrattenuta, setImportoCauzioneTrattenuta] = useState('')

  return (
    <Dialog open onClose={onAnnulla} maxWidth="xs" fullWidth>
      <DialogTitle>Check-out{prenotazione.numeroPrenotazione ? ` — #${prenotazione.numeroPrenotazione}` : ''}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <DialogContentText>Questa prenotazione prevede una cauzione.</DialogContentText>
        <FormControlLabel
          control={<Checkbox checked={restituisciCauzione} onChange={(e) => setRestituisciCauzione(e.target.checked)} disabled={inCorso} />}
          label="Restituisci l'intera cauzione al cliente"
        />
        {!restituisciCauzione && (
          <TextField
            label="Importo cauzione trattenuta (€)"
            type="number"
            value={importoCauzioneTrattenuta}
            onChange={(e) => setImportoCauzioneTrattenuta(e.target.value)}
            disabled={inCorso}
            autoFocus
          />
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onAnnulla} disabled={inCorso}>
          Annulla
        </Button>
        <Button
          variant="contained"
          color="primary"
          onClick={() =>
            onConferma(restituisciCauzione, restituisciCauzione ? null : importoCauzioneTrattenuta.trim() === '' ? null : Number(importoCauzioneTrattenuta))
          }
          disabled={inCorso}
        >
          Conferma check-out
        </Button>
      </DialogActions>
    </Dialog>
  )
}
