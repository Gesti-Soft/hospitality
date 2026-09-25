import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Skeleton from '@mui/material/Skeleton'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../../struttura/StrutturaContext'
import { StatoTicket, useChiudiTicket, useTickets, type ContestoAssistenza, type TicketDto } from '../../api/assistenza'
import { ApiError } from '../../api/client'
import { fontDisplay, tokens } from '../../theme'
import { useMobile } from '../../lib/useMobile'
import { useToast } from '../../toast/ToastContext'
import { MessaggioVuotoElenco } from '../../components/CardElenco'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { ConversazioneTicket } from '../../components/assistenza/ConversazioneTicket'
import { ElencoTicket } from '../../components/assistenza/ElencoTicket'
// Stessa altezza della pagina Assistenza della struttura.
const ALTEZZA_RIQUADRO_ASSISTENZA = 'calc(100vh - 190px)'

const CONTESTO_STAFF: ContestoAssistenza = { tipo: 'staff' }

/** Ticket di tutti i Clienti: risposta, e chiusura che cancella le foto dal server. */
export function SuperAdminAssistenzaPage() {
  const mobile = useMobile()
  const toast = useToast()
  const { isSuperAdmin } = useStruttura()
  const [parametri, setParametri] = useSearchParams()
  const [soloAperti, setSoloAperti] = useState(true)
  const tickets = useTickets(isSuperAdmin ? CONTESTO_STAFF : null, soloAperti)
  const chiudi = useChiudiTicket()
  const [daChiudere, setDaChiudere] = useState<TicketDto | null>(null)

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  const elenco = tickets.data ?? []
  const richiesto = parametri.get('ticket')
  // Un ticket aperto dal link dell'email resta raggiungibile anche se è già chiuso e il filtro
  // mostra solo gli aperti: la conversazione si carica per id, non dall'elenco.
  const selezionato = richiesto ?? (mobile ? null : (elenco[0]?.id ?? null))
  const ticketSelezionato = elenco.find((t) => t.id === selezionato)

  function seleziona(ticketId: string | null) {
    const nuovi = new URLSearchParams(parametri)
    if (ticketId) nuovi.set('ticket', ticketId)
    else nuovi.delete('ticket')
    setParametri(nuovi, { replace: true })
  }

  function confermaChiusura() {
    if (!daChiudere) return
    chiudi.mutate(daChiudere.id, {
      onSuccess: () => {
        toast.successo(`Ticket n. ${daChiudere.numero} chiuso, foto cancellate dal server.`)
        setDaChiudere(null)
      },
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Chiusura non riuscita, riprova.'),
    })
  }

  const mostraElenco = !mobile || !selezionato

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1.5, flexWrap: 'wrap' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Ticket dei clienti</Typography>
        <ToggleButtonGroup size="small" exclusive value={soloAperti ? 'aperti' : 'tutti'} onChange={(_, v) => v && setSoloAperti(v === 'aperti')}>
          <ToggleButton value="aperti">Aperti</ToggleButton>
          <ToggleButton value="tutti">Tutti</ToggleButton>
        </ToggleButtonGroup>
      </Box>

      {tickets.isLoading && <Skeleton variant="rounded" height={320} />}

      {tickets.isError && (
        <Alert severity="error">{tickets.error instanceof ApiError ? tickets.error.message : 'Impossibile caricare i ticket.'}</Alert>
      )}

      {!tickets.isLoading && !tickets.isError && elenco.length === 0 && !selezionato && (
        <MessaggioVuotoElenco messaggio={soloAperti ? 'Nessun ticket aperto.' : 'Nessun ticket ricevuto finora.'} />
      )}

      {!tickets.isLoading && (elenco.length > 0 || !!selezionato) && (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: mobile ? '1fr' : '360px 1fr',
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
              {elenco.length === 0 ? (
                <Typography sx={{ p: 2.5, fontSize: 13, color: tokens.textSecondary }}>Nessun ticket in questo elenco.</Typography>
              ) : (
                <ElencoTicket tickets={elenco} selezionato={selezionato} perStaff onSeleziona={seleziona} />
              )}
            </Box>
          )}
          {selezionato && (
            <Box sx={{ minHeight: 0, height: mobile ? 'calc(100vh - 160px)' : '100%' }}>
              <ConversazioneTicket
                key={selezionato}
                contesto={CONTESTO_STAFF}
                ticketId={selezionato}
                perStaff
                onIndietro={mobile ? () => seleziona(null) : undefined}
                azioni={
                  ticketSelezionato?.stato === StatoTicket.Aperto ? (
                    <Button size="small" variant="outlined" color="error" onClick={() => setDaChiudere(ticketSelezionato)}>
                      Chiudi ticket
                    </Button>
                  ) : undefined
                }
              />
            </Box>
          )}
        </Box>
      )}

      {daChiudere && (
        <ConfirmDialog
          titolo={`Chiudi il ticket n. ${daChiudere.numero}`}
          messaggio="Il cliente non potrà più scrivere su questo ticket e tutte le foto allegate verranno cancellate dal server, senza possibilità di recuperarle. Il testo dei messaggi resta."
          testoConferma="Chiudi e cancella le foto"
          inCorso={chiudi.isPending}
          onConferma={confermaChiusura}
          onAnnulla={() => setDaChiudere(null)}
        />
      )}
    </Box>
  )
}
