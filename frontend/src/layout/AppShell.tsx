import { useState, type MouseEvent, type ReactNode } from 'react'
import { Link as RouterLink, useLocation } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/AddOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import { useAuth } from '../auth/AuthContext'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAggiornaStruttura, useCreaStruttura, useImpostaAttivoStruttura } from '../api/strutture'
import { ApiError } from '../api/client'
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
  const [nuovaStrutturaAperta, setNuovaStrutturaAperta] = useState(false)
  const [strutturaInModifica, setStrutturaInModifica] = useState<{ id: string; nome: string } | null>(null)
  const [strutturaDaEliminare, setStrutturaDaEliminare] = useState<{ id: string; nome: string } | null>(null)

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
              onAggiungi={clienteId ? () => setNuovaStrutturaAperta(true) : undefined}
              onModificaOpzione={setStrutturaInModifica}
              onEliminaOpzione={setStrutturaDaEliminare}
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
            onAggiungi={() => setNuovaStrutturaAperta(true)}
            onModificaOpzione={setStrutturaInModifica}
            onEliminaOpzione={setStrutturaDaEliminare}
          />
        )}

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.25, overflowY: 'auto' }}>
          {navSections
            .filter((section) => !section.soloSuperAdmin || isSuperAdmin)
            // Per il SuperAdmin, le sezioni operative restano nascoste finché non seleziona
            // esplicitamente una Struttura (nessuna struttura precaricata all'accesso).
            .filter((section) => section.soloSuperAdmin || !isSuperAdmin || !!strutturaId)
            .map((section) => (
            <Box key={section.title} sx={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
              <Typography sx={sezioneLabelSx}>{section.title}</Typography>
              {section.items
                .filter((item) => !item.richiedeServizio || strutturaCorrente?.[item.richiedeServizio] !== false)
                .map((item) => {
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

      {nuovaStrutturaAperta && (
        <NuovaStrutturaDialog
          clienteId={isSuperAdmin ? clienteId : null}
          onClose={() => setNuovaStrutturaAperta(false)}
          onCreata={selezionaStruttura}
        />
      )}
      {strutturaInModifica && <ModificaStrutturaDialog struttura={strutturaInModifica} onClose={() => setStrutturaInModifica(null)} />}
      {strutturaDaEliminare && <EliminaStrutturaDialog struttura={strutturaDaEliminare} onClose={() => setStrutturaDaEliminare(null)} />}
    </Box>
  )
}

function NuovaStrutturaDialog({
  clienteId,
  onClose,
  onCreata,
}: {
  clienteId: string | null
  onClose: () => void
  onCreata: (id: string) => void
}) {
  const [nome, setNome] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const crea = useCreaStruttura()
  const queryClient = useQueryClient()

  function salva() {
    if (nome.trim() === '') {
      setErrore('Indica il nome della struttura.')
      return
    }
    setErrore(null)
    crea.mutate(
      { nome: nome.trim(), clienteId },
      {
        onSuccess: (struttura) => {
          queryClient.invalidateQueries({ queryKey: ['strutture'] })
          onCreata(struttura.id)
          onClose()
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuova struttura</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <TextField
          label="Nome struttura"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          fullWidth
          disabled={crea.isPending}
          autoFocus
          onKeyDown={(e) => e.key === 'Enter' && salva()}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={crea.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={crea.isPending}>
          Crea
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function ModificaStrutturaDialog({ struttura, onClose }: { struttura: { id: string; nome: string }; onClose: () => void }) {
  const [nome, setNome] = useState(struttura.nome)
  const [errore, setErrore] = useState<string | null>(null)
  const aggiorna = useAggiornaStruttura()
  const queryClient = useQueryClient()

  function salva() {
    if (nome.trim() === '') {
      setErrore('Indica il nome della struttura.')
      return
    }
    setErrore(null)
    aggiorna.mutate(
      { strutturaId: struttura.id, nome: nome.trim() },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: ['strutture'] })
          onClose()
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Modifica struttura</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <TextField
          label="Nome struttura"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          autoFocus
          onKeyDown={(e) => e.key === 'Enter' && salva()}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={aggiorna.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function EliminaStrutturaDialog({ struttura, onClose }: { struttura: { id: string; nome: string }; onClose: () => void }) {
  const [errore, setErrore] = useState<string | null>(null)
  const impostaAttivo = useImpostaAttivoStruttura()
  const queryClient = useQueryClient()

  function elimina() {
    setErrore(null)
    impostaAttivo.mutate(
      { strutturaId: struttura.id, attivo: false },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: ['strutture'] })
          onClose()
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Elimina struttura</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Alert severity="warning">
          Stai per eliminare <strong>{struttura.nome}</strong>. Non sarà più utilizzabile né visibile nel gestionale, e nessuno dei suoi
          utenti potrà più accedervi.
        </Alert>
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>
          Camere, prenotazioni, ospiti, fatture e tutti gli altri dati collegati NON vengono cancellati: restano conservati e recuperabili
          contattando l'assistenza, in caso di errore.
        </Typography>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={impostaAttivo.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="error" onClick={elimina} disabled={impostaAttivo.isPending}>
          Elimina struttura
        </Button>
      </DialogActions>
    </Dialog>
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
  onAggiungi?: () => void
  onModificaOpzione?: (opzione: { id: string; nome: string }) => void
  onEliminaOpzione?: (opzione: { id: string; nome: string }) => void
}

function SelettoreScuro({
  etichetta,
  valore,
  opzioni,
  caricamento,
  onChange,
  onAggiungi,
  onModificaOpzione,
  onEliminaOpzione,
}: SelettoreScuroProps) {
  const [aperto, setAperto] = useState(false)

  if (caricamento) {
    return <Skeleton variant="rounded" height={54} sx={{ bgcolor: 'rgba(255,255,255,0.06)' }} />
  }

  function azione(e: MouseEvent, callback: () => void) {
    e.stopPropagation()
    setAperto(false)
    callback()
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 0.25 }}>
        <Typography sx={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '.07em', color: '#7E899A', textTransform: 'uppercase' }}>
          {etichetta}
        </Typography>
        {onAggiungi && (
          <Tooltip title="Aggiungi nuova struttura">
            <IconButton size="small" onClick={onAggiungi} sx={{ color: '#7E899A', p: 0.25, '&:hover': { color: '#fff' } }}>
              <AddIcon sx={{ fontSize: 15 }} />
            </IconButton>
          </Tooltip>
        )}
      </Box>
      <Select
        value={valore && opzioni.some((o) => o.id === valore) ? valore : ''}
        onChange={(e) => onChange(e.target.value)}
        open={aperto}
        onOpen={() => setAperto(true)}
        onClose={() => setAperto(false)}
        displayEmpty
        disabled={opzioni.length === 0}
        renderValue={(id) => opzioni.find((o) => o.id === id)?.nome ?? ''}
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
          <MenuItem key={o.id} value={o.id} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{o.nome}</Box>
            {onModificaOpzione && (
              <IconButton size="small" onClick={(e) => azione(e, () => onModificaOpzione(o))}>
                <EditOutlinedIcon sx={{ fontSize: 16 }} />
              </IconButton>
            )}
            {onEliminaOpzione && (
              <IconButton size="small" onClick={(e) => azione(e, () => onEliminaOpzione(o))}>
                <DeleteOutlineIcon sx={{ fontSize: 16 }} />
              </IconButton>
            )}
          </MenuItem>
        ))}
      </Select>
    </Box>
  )
}
