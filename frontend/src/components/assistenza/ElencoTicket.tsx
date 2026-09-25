import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'
import { StatoTicket, type TicketDto } from '../../api/assistenza'
import { stileImporto, tokens } from '../../theme'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })

/** Stato leggibile dal punto di vista di chi guarda: "In attesa di risposta" vuol dire cose diverse per la struttura e per lo staff. */
export function ChipStatoTicket({ ticket, perStaff }: { ticket: TicketDto; perStaff: boolean }) {
  if (ticket.stato === StatoTicket.Chiuso) {
    return <Chip size="small" label="Chiuso" sx={{ bgcolor: tokens.paper, color: tokens.textSecondary, fontWeight: 700 }} />
  }

  const tocca = perStaff ? !ticket.ultimoMessaggioDaStaff : ticket.ultimoMessaggioDaStaff
  return tocca ? (
    <Chip size="small" label={perStaff ? 'Da rispondere' : 'Risposto'} sx={{ bgcolor: tokens.orange100, color: tokens.orange700, fontWeight: 700 }} />
  ) : (
    <Chip size="small" label={perStaff ? 'In attesa del cliente' : 'In attesa di risposta'} sx={{ bgcolor: tokens.blue100, color: tokens.blue700, fontWeight: 700 }} />
  )
}

export function ElencoTicket({
  tickets,
  selezionato,
  perStaff,
  onSeleziona,
}: {
  tickets: TicketDto[]
  selezionato: string | null
  perStaff: boolean
  onSeleziona: (ticketId: string) => void
}) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column' }}>
      {tickets.map((t) => {
        const attivo = t.id === selezionato
        return (
          <Box
            key={t.id}
            component="button"
            type="button"
            onClick={() => onSeleziona(t.id)}
            sx={{
              display: 'flex',
              gap: 1,
              width: '100%',
              textAlign: 'left',
              px: 2,
              py: 1.5,
              border: 'none',
              borderBottom: `1px solid ${tokens.surfaceBorder}`,
              borderLeft: `3px solid ${attivo ? tokens.blue600 : 'transparent'}`,
              bgcolor: attivo ? tokens.blue100 : 'transparent',
              cursor: 'pointer',
              font: 'inherit',
              color: 'inherit',
              '&:hover': { bgcolor: attivo ? tokens.blue100 : 'rgba(0,0,0,0.03)' },
            }}
          >
            <Box sx={{ flex: '0 0 auto', pt: 0.75 }}>
              <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: t.nonLetto ? tokens.blue600 : 'transparent' }} />
            </Box>
            <Box sx={{ minWidth: 0, flex: 1, display: 'flex', flexDirection: 'column', gap: 0.5 }}>
              <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
                <Typography sx={{ ...stileImporto, fontSize: 12, color: tokens.textTertiary, flex: '0 0 auto' }}>n. {t.numero}</Typography>
                <Typography noWrap sx={{ fontSize: 13.5, fontWeight: t.nonLetto ? 800 : 600, minWidth: 0 }}>
                  {t.oggetto}
                </Typography>
              </Box>
              {perStaff && (
                <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary }}>
                  {t.clienteRagioneSociale} · {t.strutturaNome}
                </Typography>
              )}
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
                <ChipStatoTicket ticket={t} perStaff={perStaff} />
                <Typography sx={{ ...stileImporto, fontSize: 11.5, color: tokens.textTertiary }}>
                  {formattatoreData.format(new Date(t.ultimoMessaggioAtUtc))}
                </Typography>
              </Box>
            </Box>
          </Box>
        )
      })}
    </Box>
  )
}
