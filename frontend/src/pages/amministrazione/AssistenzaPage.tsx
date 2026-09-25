import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useTickets, type ContestoAssistenza } from '../../api/assistenza'
import { ApiError } from '../../api/client'
import { fontDisplay, tokens } from '../../theme'
import { useMobile } from '../../lib/useMobile'
import { BottoneNuovo, MessaggioVuotoElenco } from '../../components/CardElenco'
import { ConversazioneTicket } from '../../components/assistenza/ConversazioneTicket'
import { ElencoTicket } from '../../components/assistenza/ElencoTicket'
import { NuovoTicketDialog } from '../../components/assistenza/NuovoTicketDialog'

/** Altezza del riquadro elenco + conversazione sotto la barra in alto e il titolo della pagina. */
const ALTEZZA_RIQUADRO_ASSISTENZA = 'calc(100vh - 190px)'

/**
 * Ticket di assistenza della struttura selezionata. La vedono il titolare e chi gestisce gli
 * utenti; le risposte dello staff arrivano qui, e per email al solo titolare.
 * `?ticket=<id>` apre direttamente un ticket (link nell'email), `?nuovo=1` il dialogo del nuovo
 * ticket (pulsante nella barra in alto).
 */
export function AssistenzaPage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const [parametri, setParametri] = useSearchParams()
  const contesto: ContestoAssistenza | null = strutturaId ? { tipo: 'struttura', strutturaId } : null
  const tickets = useTickets(contesto)
  const [nuovoAperto, setNuovoAperto] = useState(parametri.get('nuovo') === '1')

  const elenco = tickets.data ?? []
  const richiesto = parametri.get('ticket')
  // Il link dell'email può riferirsi a un'altra struttura dello stesso Cliente: se il ticket non è
  // tra quelli di questa, si mostra l'elenco invece di un errore.
  const selezionato = elenco.some((t) => t.id === richiesto) ? richiesto : mobile ? null : (elenco[0]?.id ?? null)

  function seleziona(ticketId: string | null) {
    const nuovi = new URLSearchParams(parametri)
    nuovi.delete('nuovo')
    if (ticketId) nuovi.set('ticket', ticketId)
    else nuovi.delete('ticket')
    setParametri(nuovi, { replace: true })
  }

  function chiudiNuovo() {
    setNuovoAperto(false)
    if (parametri.has('nuovo')) {
      const nuovi = new URLSearchParams(parametri)
      nuovi.delete('nuovo')
      setParametri(nuovi, { replace: true })
    }
  }

  if (!contesto) {
    return <Alert severity="info">Seleziona una struttura per vedere i ticket di assistenza.</Alert>
  }

  const mostraElenco = !mobile || !selezionato
  const mostraConversazione = !!selezionato

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1.5 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Richieste all&apos;assistenza GestiSoft</Typography>
        <BottoneNuovo etichetta="+ Nuovo ticket" onClick={() => setNuovoAperto(true)} />
      </Box>

      {tickets.isLoading && <Skeleton variant="rounded" height={320} />}

      {tickets.isError && (
        <Alert severity="error">{tickets.error instanceof ApiError ? tickets.error.message : 'Impossibile caricare i ticket.'}</Alert>
      )}

      {!tickets.isLoading && !tickets.isError && elenco.length === 0 && (
        <MessaggioVuotoElenco messaggio="Nessun ticket. Se qualcosa non funziona o hai un dubbio, apri un ticket: ti rispondiamo qui." />
      )}

      {!tickets.isLoading && elenco.length > 0 && (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: mobile ? '1fr' : '340px 1fr',
            height: mobile ? 'auto' : ALTEZZA_RIQUADRO_ASSISTENZA,
            minHeight: mobile ? undefined : 480,
            border: `1px solid ${tokens.surfaceBorder}`,
            borderRadius: 2,
            bgcolor: tokens.surface,
            overflow: 'hidden',
          }}
        >
          {mostraElenco && (
            <Box sx={{ borderRight: mobile ? 'none' : `1px solid ${tokens.surfaceBorder}`, overflowY: 'auto', minHeight: 0 }}>
              <ElencoTicket tickets={elenco} selezionato={selezionato} perStaff={false} onSeleziona={seleziona} />
            </Box>
          )}
          {mostraConversazione && (
            <Box sx={{ minHeight: 0, height: mobile ? 'calc(100vh - 160px)' : '100%' }}>
              <ConversazioneTicket
                key={selezionato}
                contesto={contesto}
                ticketId={selezionato}
                perStaff={false}
                onIndietro={mobile ? () => seleziona(null) : undefined}
              />
            </Box>
          )}
        </Box>
      )}

      {nuovoAperto && strutturaId && (
        <NuovoTicketDialog
          strutturaId={strutturaId}
          onClose={chiudiNuovo}
          onAperto={(dettaglio) => {
            setNuovoAperto(false)
            seleziona(dettaglio.ticket.id)
          }}
        />
      )}
    </Box>
  )
}
