import { useEffect, useRef, useState, type ReactNode } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import ArrowBackIcon from '@mui/icons-material/ArrowBackOutlined'
import { ApiError } from '../../api/client'
import {
  StatoTicket,
  TESTO_MAX_CARATTERI,
  useRispondiTicket,
  useSegnaTicketLetto,
  useTicket,
  type ContestoAssistenza,
  type TicketMessaggioDto,
} from '../../api/assistenza'
import { fontDisplay, stileImporto, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { ChipStatoTicket } from './ElencoTicket'
import { FotoAllegata } from './FotoAllegata'
import { SelettoreFoto } from './SelettoreFoto'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

/** Dettaglio di un ticket: messaggi in ordine, foto e risposta. Aprirlo lo segna come letto. Va montato con key={ticketId}: cambiando ticket la bozza non deve restare. */
export function ConversazioneTicket({
  contesto,
  ticketId,
  perStaff,
  azioni,
  onIndietro,
}: {
  contesto: ContestoAssistenza
  ticketId: string
  perStaff: boolean
  /** Pulsanti aggiuntivi nella testata (per lo staff: "Chiudi ticket"). */
  azioni?: ReactNode
  /** Su telefono si torna all'elenco da qui. */
  onIndietro?: () => void
}) {
  const toast = useToast()
  const dettaglio = useTicket(contesto, ticketId)
  const segnaLetto = useSegnaTicketLetto(contesto)
  const rispondi = useRispondiTicket(contesto)
  const [testo, setTesto] = useState('')
  const [foto, setFoto] = useState<File[]>([])
  const fondo = useRef<HTMLDivElement | null>(null)

  const ticket = dettaglio.data?.ticket
  const numeroMessaggi = dettaglio.data?.messaggi.length ?? 0

  // Una sola chiamata per apertura: dopo, la lista ricaricata dice già "letto".
  const nonLetto = ticket?.nonLetto === true
  const { mutate: segna } = segnaLetto
  useEffect(() => {
    if (nonLetto) segna(ticketId)
  }, [nonLetto, ticketId, segna])

  useEffect(() => {
    fondo.current?.scrollIntoView({ block: 'end' })
  }, [ticketId, numeroMessaggi])

  if (dettaglio.isLoading) {
    return <Skeleton variant="rounded" height={320} />
  }

  if (dettaglio.isError || !dettaglio.data || !ticket) {
    return <Alert severity="error">{dettaglio.error instanceof ApiError ? dettaglio.error.message : 'Ticket non disponibile.'}</Alert>
  }

  const aperto = ticket.stato === StatoTicket.Aperto

  function invia() {
    rispondi.mutate(
      { ticketId, testo: testo.trim(), allegati: foto },
      {
        onSuccess: () => {
          setTesto('')
          setFoto([])
          toast.successo('Messaggio inviato.')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Invio non riuscito, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: 0, height: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5, px: 2.5, py: 2, borderBottom: `1px solid ${tokens.surfaceBorder}`, flexWrap: 'wrap' }}>
        {onIndietro && (
          <Button size="small" startIcon={<ArrowBackIcon fontSize="small" />} onClick={onIndietro} sx={{ ml: -1 }}>
            Ticket
          </Button>
        )}
        <Box sx={{ minWidth: 0, flex: '1 1 240px' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>
            <Box component="span" sx={{ ...stileImporto, color: tokens.textTertiary, fontWeight: 600, mr: 1 }}>
              n. {ticket.numero}
            </Box>
            {ticket.oggetto}
          </Typography>
          <Typography sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
            {perStaff ? `${ticket.clienteRagioneSociale} · ${ticket.strutturaNome} · ` : ''}
            aperto il {formattatoreDataOra.format(new Date(ticket.createdAtUtc))}
            {ticket.chiusoAtUtc ? ` · chiuso il ${formattatoreDataOra.format(new Date(ticket.chiusoAtUtc))}` : ''}
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <ChipStatoTicket ticket={ticket} perStaff={perStaff} />
          {azioni}
        </Box>
      </Box>

      <Box sx={{ flex: 1, minHeight: 0, overflowY: 'auto', px: 2.5, py: 2, display: 'flex', flexDirection: 'column', gap: 1.5, bgcolor: tokens.paper }}>
        {ticket.anonimizzatoAtUtc && (
          <Typography sx={{ fontSize: 13, color: tokens.textSecondary, textAlign: 'center', py: 2 }}>
            I messaggi di questo ticket sono stati cancellati il {formattatoreDataOra.format(new Date(ticket.anonimizzatoAtUtc))}, 12 mesi
            dopo la chiusura, come prevede la conservazione dei dati. Restano solo numero e oggetto.
          </Typography>
        )}
        {dettaglio.data.messaggi.map((m) => (
          <Messaggio key={m.id} messaggio={m} contesto={contesto} mio={m.daStaff === perStaff} />
        ))}
        <div ref={fondo} />
      </Box>

      <Box sx={{ borderTop: `1px solid ${tokens.surfaceBorder}`, px: 2.5, py: 2, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
        {aperto ? (
          <>
            <TextField
              multiline
              minRows={2}
              maxRows={8}
              fullWidth
              placeholder={perStaff ? 'Scrivi la risposta al cliente…' : 'Scrivi un messaggio all’assistenza…'}
              value={testo}
              onChange={(e) => setTesto(e.target.value)}
              disabled={rispondi.isPending}
              slotProps={{ htmlInput: { maxLength: TESTO_MAX_CARATTERI } }}
            />
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1.5, flexWrap: 'wrap' }}>
              <SelettoreFoto foto={foto} onChange={setFoto} disabilitato={rispondi.isPending} />
              <Button variant="contained" onClick={invia} disabled={rispondi.isPending || testo.trim().length === 0}>
                {rispondi.isPending ? 'Invio…' : 'Invia'}
              </Button>
            </Box>
          </>
        ) : (
          <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>
            {perStaff
              ? 'Ticket chiuso: non accetta più messaggi e le foto sono state cancellate dal server.'
              : 'Questo ticket è chiuso. Se il problema si ripresenta, apri un ticket nuovo.'}
          </Typography>
        )}
      </Box>
    </Box>
  )
}

function Messaggio({ messaggio, contesto, mio }: { messaggio: TicketMessaggioDto; contesto: ContestoAssistenza; mio: boolean }) {
  return (
    <Box sx={{ alignSelf: mio ? 'flex-end' : 'flex-start', maxWidth: { xs: '92%', md: '78%' }, display: 'flex', flexDirection: 'column', gap: 0.5 }}>
      <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary, textAlign: mio ? 'right' : 'left' }}>
        <Box component="span" sx={{ fontWeight: 700, color: messaggio.daStaff ? tokens.orange700 : tokens.textSecondary }}>
          {messaggio.autore}
        </Box>
        {' · '}
        {formattatoreDataOra.format(new Date(messaggio.createdAtUtc))}
      </Typography>
      <Box
        sx={{
          bgcolor: mio ? tokens.blue100 : tokens.surface,
          border: `1px solid ${mio ? 'transparent' : tokens.surfaceBorder}`,
          borderRadius: 2,
          px: 1.75,
          py: 1.25,
          display: 'flex',
          flexDirection: 'column',
          gap: 1,
        }}
      >
        <Typography sx={{ fontSize: 13.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{messaggio.testo}</Typography>
        {messaggio.allegati.length > 0 && (
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            {messaggio.allegati.map((a) => (
              <FotoAllegata key={a.id} contesto={contesto} allegato={a} />
            ))}
          </Box>
        )}
      </Box>
    </Box>
  )
}
