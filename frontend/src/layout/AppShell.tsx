import type { ReactNode } from 'react'
import { Link as RouterLink, useLocation } from 'react-router-dom'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Skeleton from '@mui/material/Skeleton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useAuth } from '../auth/AuthContext'
import { useStruttura } from '../struttura/StrutturaContext'
import { fontDisplay, tokens } from '../theme'
import { GestiSoftMark } from '../components/GestiSoftMark'
import { navItemsFlat, navSections } from './navItems'
import { IconEsci } from './navIcons'

const NAV_RAIL_WIDTH = 250

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation()
  const { sessione, esci } = useAuth()
  const { isSuperAdmin, clienti, clienteId, strutture, strutturaId, strutturaCorrente, loading, selezionaCliente, selezionaStruttura } =
    useStruttura()

  const paginaCorrente = navItemsFlat.find((item) => item.path === location.pathname)
  const inizialiUtente = (sessione?.email ?? '?').slice(0, 2).toUpperCase()

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: tokens.paper }}>
      {/* Nav rail */}
      <Box
        component="nav"
        sx={{
          flex: `0 0 ${NAV_RAIL_WIDTH}px`,
          bgcolor: tokens.navRail,
          display: 'flex',
          flexDirection: 'column',
          p: '22px 14px',
          gap: '20px',
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, px: 1 }}>
          <GestiSoftMark size={30} />
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, color: '#fff' }}>GestiSoft</Typography>
        </Box>

        {isSuperAdmin && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.75 }}>
            <Typography sx={selettoreLabelSx}>Vista Super Admin</Typography>

            <SelettoreScuro
              etichetta="Cliente"
              valore={clienteId}
              opzioni={clienti.map((c) => ({ id: c.id, nome: c.ragioneSociale }))}
              caricamento={loading && clienti.length === 0}
              onChange={selezionaCliente}
            />
            <SelettoreScuro
              etichetta="Struttura"
              valore={strutturaId}
              opzioni={strutture.map((s) => ({ id: s.id, nome: s.nome }))}
              caricamento={loading && strutture.length === 0}
              onChange={selezionaStruttura}
            />
          </Box>
        )}

        {!isSuperAdmin && (
          <SelettoreScuro
            etichetta="Struttura"
            valore={strutturaId}
            opzioni={strutture.map((s) => ({ id: s.id, nome: s.nome }))}
            caricamento={loading && strutture.length === 0}
            onChange={selezionaStruttura}
          />
        )}

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25, overflowY: 'auto' }}>
          {navSections.map((section) => (
            <Box key={section.title} sx={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
              <Typography sx={sezioneLabelSx}>{section.title}</Typography>
              {section.items.map((item) => {
                const attivo = item.path === location.pathname
                const Icon = item.icon
                return (
                  <Box
                    key={item.path}
                    component={RouterLink}
                    to={item.path}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '12px',
                      px: '14px',
                      py: '9px',
                      borderRadius: '10px',
                      fontSize: 13.5,
                      fontWeight: 600,
                      textDecoration: 'none',
                      color: attivo ? '#fff' : tokens.navText,
                      bgcolor: attivo ? '#2A3745' : 'transparent',
                      '&:hover': { bgcolor: attivo ? '#2A3745' : 'rgba(255,255,255,0.04)' },
                    }}
                  >
                    <Icon width={17} height={17} style={{ opacity: attivo ? 1 : 0.85 }} />
                    {item.label}
                  </Box>
                )
              })}
            </Box>
          ))}
        </Box>

        <Box sx={{ mt: 'auto', display: 'flex', alignItems: 'center', gap: 1.25, p: 1.25, borderTop: '1px solid #262C36' }}>
          <Box
            sx={{
              width: 30,
              height: 30,
              borderRadius: '8px',
              bgcolor: tokens.orange600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontFamily: fontDisplay,
              fontWeight: 700,
              color: '#fff',
              fontSize: 12.5,
              flex: '0 0 auto',
            }}
          >
            {inizialiUtente}
          </Box>
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography noWrap sx={{ fontSize: 12.5, fontWeight: 700, color: '#fff' }}>
              {sessione?.email}
            </Typography>
            <Typography sx={{ fontSize: 10.5, color: '#7E899A' }}>{sessione?.isSuperAdmin ? 'Super Admin' : 'Operatore'}</Typography>
          </Box>
          <Tooltip title="Esci">
            <IconButton size="small" onClick={esci} sx={{ color: '#7E899A' }}>
              <IconEsci />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {/* Contenuto */}
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            px: 4.5,
            py: 2.75,
            borderBottom: `1px solid ${tokens.surfaceBorder}`,
            bgcolor: tokens.surface,
          }}
        >
          <Box>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 22 }}>{paginaCorrente?.label ?? 'GestiSoft'}</Typography>
            {strutturaCorrente ? (
              <Typography sx={{ fontSize: 13, color: tokens.textSecondary, mt: 0.375 }}>{strutturaCorrente.nome}</Typography>
            ) : (
              <Skeleton width={140} height={18} sx={{ mt: 0.375 }} />
            )}
          </Box>
        </Box>

        <Box sx={{ flex: 1, overflow: 'auto', p: 4.5 }}>{children}</Box>
      </Box>
    </Box>
  )
}

const selettoreLabelSx = {
  fontSize: 9.5,
  fontWeight: 700,
  letterSpacing: '.07em',
  color: '#5C6675',
  textTransform: 'uppercase' as const,
  px: 0.25,
}

const sezioneLabelSx = {
  fontSize: 10.5,
  fontWeight: 700,
  letterSpacing: '.08em',
  color: '#5C6675',
  textTransform: 'uppercase' as const,
  px: '14px',
  pt: '14px',
  pb: '6px',
}

interface SelettoreScuroProps {
  etichetta: string
  valore: string | null
  opzioni: { id: string; nome: string }[]
  caricamento: boolean
  onChange: (id: string) => void
}

function SelettoreScuro({ etichetta, valore, opzioni, caricamento, onChange }: SelettoreScuroProps) {
  if (caricamento) {
    return <Skeleton variant="rounded" height={54} sx={{ bgcolor: 'rgba(255,255,255,0.06)' }} />
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
      <Typography sx={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '.07em', color: '#7E899A', textTransform: 'uppercase', px: 0.25 }}>
        {etichetta}
      </Typography>
      <Select
        value={valore && opzioni.some((o) => o.id === valore) ? valore : ''}
        onChange={(e) => onChange(e.target.value)}
        displayEmpty
        disabled={opzioni.length === 0}
        sx={{
          bgcolor: '#20262F',
          color: '#fff',
          fontSize: 13,
          fontWeight: 700,
          borderRadius: '10px',
          '.MuiOutlinedInput-notchedOutline': { borderColor: '#2C333D' },
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: '#2C333D' },
          '.MuiSvgIcon-root': { color: '#7E899A' },
        }}
      >
        {opzioni.length === 0 && <MenuItem value="">Nessuna struttura disponibile</MenuItem>}
        {opzioni.map((o) => (
          <MenuItem key={o.id} value={o.id}>
            {o.nome}
          </MenuItem>
        ))}
      </Select>
    </Box>
  )
}
