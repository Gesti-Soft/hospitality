import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Badge from '@mui/material/Badge'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Popover from '@mui/material/Popover'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { StatoTicket, useContoTicketNonLetti, useTickets, type ContestoAssistenza, type TicketDto } from '../../api/assistenza'
import { useMioPermessoStruttura } from '../../api/utenti'
import { useStruttura } from '../../struttura/StrutturaContext'
import { IconAssistenza } from '../../layout/navIcons'
import { stileImporto, tokens } from '../../theme'

const TICKET_NEL_PANNELLO = 5

/**
 * Icona Assistenza nella barra in alto: le risposte non lette e "Apri nuovo ticket" per chi può
 * aprirne (titolare e chi gestisce gli utenti della struttura), i ticket da rispondere per il Super
 * Admin nel suo pannello. Per tutti gli altri non compare.
 */
export function PannelloAssistenza() {
  const navigate = useNavigate()
  const { isSuperAdmin, strutturaId } = useStruttura()
  const mioPermesso = useMioPermessoStruttura(!isSuperAdmin ? strutturaId : null)
  const [ancora, setAncora] = useState<HTMLElement | null>(null)

  // Il Super Admin entrato in una struttura sta operando come quel Cliente: il suo pannello (e
  // quindi la pagina a cui porterebbe questa icona) in quel momento non è raggiungibile.
  const contesto: ContestoAssistenza | null = isSuperAdmin
    ? strutturaId
      ? null
      : { tipo: 'staff' }
    : strutturaId && mioPermesso.data?.settingUser === true
      ? { tipo: 'struttura', strutturaId }
      : null

  const conteggio = useContoTicketNonLetti(contesto)
  const tickets = useTickets(contesto, true)

  if (!contesto) {
    return null
  }

  const perStaff = contesto.tipo === 'staff'
  const paginaAssistenza = perStaff ? '/super-admin/assistenza' : '/assistenza'
  const inEvidenza = (tickets.data ?? [])
    .filter((t) => t.stato === StatoTicket.Aperto)
    .sort((a, b) => Number(b.nonLetto) - Number(a.nonLetto))
    .slice(0, TICKET_NEL_PANNELLO)

  function vai(percorso: string) {
    setAncora(null)
    navigate(percorso)
  }

  return (
    <>
      <Tooltip title="Assistenza">
        <IconButton size="small" onClick={(e) => setAncora(e.currentTarget)} sx={{ color: '#7E899A' }}>
          <Badge badgeContent={conteggio.data ?? 0} color="error" max={99} sx={{ '& .MuiBadge-badge': { fontSize: 9.5, fontWeight: 700 } }}>
            <IconAssistenza width={19} height={19} />
          </Badge>
        </IconButton>
      </Tooltip>

      <Popover
        open={!!ancora}
        anchorEl={ancora}
        onClose={() => setAncora(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 360, maxWidth: 'calc(100vw - 32px)', bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}` } } }}
      >
        <Box sx={{ px: 2, py: 1.5, borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
          <Typography sx={{ fontWeight: 800, fontSize: 14 }}>Assistenza</Typography>
          <Typography sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
            {perStaff ? 'Ticket aperti dei clienti.' : 'Le risposte dell’assistenza GestiSoft ai tuoi ticket.'}
          </Typography>
        </Box>

        <Box sx={{ maxHeight: 320, overflowY: 'auto' }}>
          {inEvidenza.length === 0 ? (
            <Typography sx={{ p: 2.5, fontSize: 13, color: tokens.textSecondary, textAlign: 'center' }}>
              Nessun ticket aperto.
            </Typography>
          ) : (
            inEvidenza.map((t) => <RigaTicket key={t.id} ticket={t} perStaff={perStaff} onClick={() => vai(`${paginaAssistenza}?ticket=${t.id}`)} />)
          )}
        </Box>

        <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1, px: 2, py: 1.5, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
          <Button size="small" onClick={() => vai(paginaAssistenza)}>
            Tutti i ticket
          </Button>
          {!perStaff && (
            <Button size="small" variant="contained" onClick={() => vai('/assistenza?nuovo=1')}>
              Apri nuovo ticket
            </Button>
          )}
        </Box>
      </Popover>
    </>
  )
}

function RigaTicket({ ticket, perStaff, onClick }: { ticket: TicketDto; perStaff: boolean; onClick: () => void }) {
  const sottotitolo = perStaff
    ? `${ticket.clienteRagioneSociale} · ${ticket.strutturaNome}`
    : ticket.nonLetto
      ? 'Nuova risposta da leggere'
      : ticket.ultimoMessaggioDaStaff
        ? 'Risposto'
        : 'In attesa di risposta'

  return (
    <Box
      component="button"
      type="button"
      onClick={onClick}
      sx={{
        display: 'flex',
        gap: 1,
        width: '100%',
        textAlign: 'left',
        px: 2,
        py: 1.25,
        border: 'none',
        borderBottom: `1px solid ${tokens.surfaceBorder}`,
        bgcolor: ticket.nonLetto ? 'rgba(28,126,168,0.06)' : 'transparent',
        cursor: 'pointer',
        font: 'inherit',
        color: 'inherit',
        '&:hover': { bgcolor: 'rgba(0,0,0,0.03)' },
      }}
    >
      <Box sx={{ flex: '0 0 auto', pt: 0.5 }}>
        <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: ticket.nonLetto ? tokens.blue600 : 'transparent' }} />
      </Box>
      <Box sx={{ minWidth: 0, flex: 1 }}>
        <Typography noWrap sx={{ fontSize: 13, fontWeight: ticket.nonLetto ? 700 : 500 }}>
          <Box component="span" sx={{ ...stileImporto, color: tokens.textTertiary, mr: 0.75 }}>
            n. {ticket.numero}
          </Box>
          {ticket.oggetto}
        </Typography>
        <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
          {sottotitolo}
        </Typography>
      </Box>
    </Box>
  )
}
