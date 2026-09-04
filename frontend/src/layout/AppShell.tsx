import { useEffect, useState, type ReactNode } from 'react'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Drawer from '@mui/material/Drawer'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import AddIcon from '@mui/icons-material/AddOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import MenuIcon from '@mui/icons-material/MenuOutlined'
import { useAuth } from '../auth/AuthContext'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAggiornaStruttura, useCreaStruttura, useImpostaAttivoStruttura, type StrutturaDto } from '../api/strutture'
import { GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA } from '../api/superAdmin'
import { useMioPermessoStruttura } from '../api/utenti'
import { ApiError } from '../api/client'
import { fontDisplay, tokens } from '../theme'
import { GestiSoftMark } from '../components/GestiSoftMark'
import { navItemsFlat, navSections, type NavSection } from './navItems'
import { IconEsci, IconSuperAdmin } from './navIcons'

const NAV_RAIL_WIDTH = 232
const TOPBAR_HEIGHT = 56

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation()
  const navigate = useNavigate()
  const { sessione, esci } = useAuth()
  const { isSuperAdmin, clienti, clienteId, strutture, strutturaId, strutturaCorrente, loading, selezionaCliente, selezionaStruttura } =
    useStruttura()
  // Solo per decidere se mostrare le voci "richiedeGestioneUtenti" (es. Log) — il Super Admin non
  // ha bisogno di questo dato, ce l'ha sempre libero.
  const mioPermesso = useMioPermessoStruttura(!isSuperAdmin ? strutturaId : null)

  const sezioniVisibili = navSections
    .filter((section) => !section.soloSuperAdmin || isSuperAdmin)
    // Per il SuperAdmin, le sezioni operative restano nascoste finché non seleziona
    // esplicitamente una Struttura (nessuna struttura precaricata all'accesso).
    .filter((section) => section.soloSuperAdmin || !isSuperAdmin || !!strutturaId)
    .filter((section) => !section.richiedeGestioneUtenti || isSuperAdmin || mioPermesso.data?.settingUser === true)
    .map((section) => ({
      section,
      // Una sezione i cui servizi sono tutti disabilitati (es. "Invii automatici" senza alcun
      // servizio esterno concesso) non deve comparire nemmeno come tab.
      voci: section.items.filter((item) => !item.richiedeServizio || strutturaCorrente?.[item.richiedeServizio] !== false),
    }))
    .filter(({ voci }) => voci.length > 0)

  const sezioneAttiva = sezioniVisibili.find(({ voci }) => voci.some((item) => item.path === location.pathname)) ?? sezioniVisibili[0]

  const paginaCorrente = navItemsFlat.find((item) => item.path === location.pathname)
  const inizialiUtente = (sessione?.email ?? '?').slice(0, 2).toUpperCase()
  const [nuovaStrutturaAperta, setNuovaStrutturaAperta] = useState(false)
  const [strutturaInModifica, setStrutturaInModifica] = useState<{ id: string; nome: string } | null>(null)
  const [strutturaDaEliminare, setStrutturaDaEliminare] = useState<{ id: string; nome: string } | null>(null)

  // Il Super Admin è "in modalità impersonazione" quando ha scelto una Struttura di un Cliente su
  // cui operare — a differenza sua, un Cliente/Operatore normale è sempre e solo nella propria.
  const clienteImpersonato = isSuperAdmin ? clienti.find((c) => c.id === clienteId) : undefined
  const inImpersonazione = isSuperAdmin && !!strutturaCorrente

  // Sotto i 900px la sidebar fissa e le tab in barra non ci stanno: diventano un menu a comparsa
  // aperto dall'icona hamburger, che raccoglie anche Cliente/tab (in barra solo su desktop).
  const mobile = useMediaQuery('(max-width:899.95px)')
  const [menuAperto, setMenuAperto] = useState(false)
  useEffect(() => setMenuAperto(false), [location.pathname])

  const selettoreCliente = isSuperAdmin && (
    <SelettoreCercabile
      etichetta="Cliente"
      valore={clienteId}
      opzioni={clienti.filter((c) => c.attivo).map((c) => ({ id: c.id, nome: c.ragioneSociale }))}
      caricamento={loading && clienti.length === 0}
      onChange={selezionaCliente}
      compatta={!mobile}
      larghezza={170}
    />
  )

  const tabSezioni = (
    <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', alignItems: mobile ? 'stretch' : 'center', gap: 0.5 }}>
      {sezioniVisibili.map(({ section, voci }) => (
        <TabSezione
          key={section.title}
          sezione={section}
          attiva={section.title === sezioneAttiva?.section.title}
          onClick={() => navigate(voci[0].path)}
          larghezzaPiena={mobile}
        />
      ))}
    </Box>
  )

  const contenutoNav = (
    <Box
      component={mobile ? 'div' : 'nav'}
      sx={{
        flex: mobile ? undefined : `0 0 ${NAV_RAIL_WIDTH}px`,
        width: mobile ? NAV_RAIL_WIDTH : undefined,
        height: mobile ? '100%' : undefined,
        bgcolor: tokens.navRail,
        display: 'flex',
        flexDirection: 'column',
        p: '18px 14px',
        gap: '16px',
        overflowY: 'auto',
      }}
    >
      {mobile && selettoreCliente}
      {mobile && tabSezioni}

      {/* Il SuperAdmin senza ancora un Cliente scelto non ha alcuna struttura sensata da mostrare
          qui: la select resta nascosta finché non ne sceglie uno (vedi selettoreCliente sopra). */}
      {(!isSuperAdmin || clienteId) && (
        <SelettoreCercabile
          etichetta="Struttura"
          valore={strutturaId}
          opzioni={strutture.map((s) => ({ id: s.id, nome: s.nome }))}
          caricamento={loading && strutture.length === 0}
          onChange={selezionaStruttura}
          onAggiungi={isSuperAdmin ? () => setNuovaStrutturaAperta(true) : undefined}
          onModificaOpzione={setStrutturaInModifica}
          onEliminaOpzione={setStrutturaDaEliminare}
        />
      )}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
        {sezioneAttiva?.voci.map((item) => {
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
    </Box>
  )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100vh', overflow: 'hidden', bgcolor: tokens.paper }}>
      {/* Barra di navigazione in alto */}
      <Box
        component="header"
        sx={{ flex: `0 0 ${TOPBAR_HEIGHT}px`, bgcolor: tokens.navRail, display: 'flex', alignItems: 'center', gap: { xs: 1.5, md: 3 }, px: { xs: 1.5, md: 3 } }}
      >
        {mobile && (
          <IconButton onClick={() => setMenuAperto(true)} sx={{ color: '#fff', flex: '0 0 auto', p: 0.75 }}>
            <MenuIcon />
          </IconButton>
        )}

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, flex: '0 0 auto' }}>
          <GestiSoftMark size={26} />
          {!mobile && <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15, color: '#fff' }}>GestiSoft</Typography>}
        </Box>

        {!mobile && selettoreCliente}
        {!mobile && tabSezioni}

        <Box sx={{ ml: 'auto', display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box
            sx={{
              width: 28,
              height: 28,
              borderRadius: '8px',
              bgcolor: tokens.orange600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontFamily: fontDisplay,
              fontWeight: 700,
              color: '#fff',
              fontSize: 11.5,
              flex: '0 0 auto',
            }}
          >
            {inizialiUtente}
          </Box>
          {!mobile && (
            <Box sx={{ minWidth: 0 }}>
              <Typography noWrap sx={{ fontSize: 12, fontWeight: 700, color: '#fff', maxWidth: 180 }}>
                {sessione?.email}
              </Typography>
              <Typography sx={{ fontSize: 10, color: '#7E899A' }}>{sessione?.isSuperAdmin ? 'Super Admin' : 'Operatore'}</Typography>
            </Box>
          )}
          <Tooltip title="Esci">
            <IconButton size="small" onClick={esci} sx={{ color: '#7E899A' }}>
              <IconEsci />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      {inImpersonazione && (
        <Box sx={{ flex: '0 0 auto', bgcolor: tokens.orange600, color: '#fff', display: 'flex', alignItems: 'center', gap: 1, px: { xs: 1.5, md: 3 }, py: 1 }}>
          <IconSuperAdmin width={15} height={15} style={{ flex: '0 0 auto' }} />
          <Typography sx={{ fontSize: 12.5 }}>
            <strong>Modalità impersonazione</strong> — Stai operando come <strong>{strutturaCorrente!.nome}</strong>
            {clienteImpersonato ? ` (${clienteImpersonato.ragioneSociale})` : ''}
          </Typography>
        </Box>
      )}

      <Box sx={{ flex: 1, display: 'flex', minHeight: 0 }}>
        {!mobile && contenutoNav}
        {mobile && (
          <Drawer
            anchor="left"
            open={menuAperto}
            onClose={() => setMenuAperto(false)}
            slotProps={{ paper: { sx: { border: 'none', bgcolor: tokens.navRail, width: NAV_RAIL_WIDTH } } }}
          >
            {contenutoNav}
          </Drawer>
        )}

        {/* Contenuto */}
        <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              px: { xs: 2.5, md: 4.5 },
              py: { xs: 2, md: 2.75 },
              borderBottom: `1px solid ${tokens.surfaceBorder}`,
              bgcolor: tokens.surface,
            }}
          >
            <Box>
              <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: { xs: 18, md: 22 } }}>{paginaCorrente?.label ?? 'GestiSoft'}</Typography>
              {/* Il Super Admin senza ancora una Struttura scelta (es. sulla propria Dashboard) non ha
                  nessuna struttura da mostrare qui sotto — niente skeleton "in caricamento" fuori luogo. */}
              {strutturaId &&
                (strutturaCorrente ? (
                  <Typography sx={{ fontSize: 13, color: tokens.textSecondary, mt: 0.375 }}>{strutturaCorrente.nome}</Typography>
                ) : (
                  <Skeleton width={140} height={18} sx={{ mt: 0.375 }} />
                ))}
            </Box>
          </Box>

          <Box sx={{ flex: 1, overflow: 'auto', p: { xs: 2, md: 4.5 } }}>{children}</Box>
        </Box>
      </Box>

      {nuovaStrutturaAperta && isSuperAdmin && clienteId && (
        <NuovaStrutturaDialog clienteId={clienteId} onClose={() => setNuovaStrutturaAperta(false)} onCreata={selezionaStruttura} />
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
  clienteId: string
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
          // Aggiorna subito la cache (non solo invalidateQueries, che rifetcha in background): lo
          // Struttura Context riconcilia strutturaId/strutture in un effetto che, senza questo,
          // vedrebbe per un istante la vecchia lista senza la nuova struttura appena selezionata e
          // azzererebbe la selezione prima ancora che il refetch arrivi (bug reale riscontrato:
          // creare una struttura da Super Admin non ci si spostava mai dentro).
          const chiave = ['strutture', clienteId ?? 'proprie']
          queryClient.setQueryData<StrutturaDto[]>(chiave, (attuali) => [...(attuali ?? []), struttura])
          queryClient.invalidateQueries({ queryKey: chiave })
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
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>
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
        <Button variant="contained" color="primary" onClick={salva} disabled={crea.isPending}>
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
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>
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
        <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
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
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>
          La struttura resterà inattiva sul server per almeno {GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA} giorni, in modo da poterla riattivare
          in caso di errore; dopodiché potrà essere rimossa in modo definitivo.
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

function TabSezione({
  sezione,
  attiva,
  onClick,
  larghezzaPiena,
}: {
  sezione: NavSection
  attiva: boolean
  onClick: () => void
  /** A tutta larghezza e allineata a sinistra, per l'elenco verticale nel menu a comparsa mobile. */
  larghezzaPiena?: boolean
}) {
  const Icon = sezione.icon
  return (
    <Box
      component="button"
      onClick={onClick}
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: larghezzaPiena ? 'flex-start' : 'center',
        width: larghezzaPiena ? '100%' : undefined,
        gap: '8px',
        px: '14px',
        py: '7px',
        border: 'none',
        borderRadius: '9px',
        fontFamily: fontDisplay,
        fontSize: 13,
        fontWeight: 700,
        cursor: 'pointer',
        color: attiva ? '#fff' : tokens.navText,
        bgcolor: attiva ? tokens.blue600 : 'transparent',
        '&:hover': { bgcolor: attiva ? tokens.blue600 : 'rgba(255,255,255,0.06)' },
      }}
    >
      <Icon width={16} height={16} style={{ opacity: attiva ? 1 : 0.85, flex: '0 0 auto' }} />
      {sezione.title}
    </Box>
  )
}

interface Opzione {
  id: string
  nome: string
}

interface SelettoreCercabileProps {
  etichetta: string
  valore: string | null
  opzioni: Opzione[]
  caricamento: boolean
  onChange: (id: string) => void
  onAggiungi?: () => void
  onModificaOpzione?: (opzione: Opzione) => void
  onEliminaOpzione?: (opzione: Opzione) => void
  /** Variante compatta senza etichetta sopra il campo, per la barra in alto (es. select Cliente). */
  compatta?: boolean
  larghezza?: number
}

/** Autocomplete cercabile in stile scuro, per le select Cliente/Struttura del Super Admin e Struttura del Cliente. */
function SelettoreCercabile({
  etichetta,
  valore,
  opzioni,
  caricamento,
  onChange,
  onAggiungi,
  onModificaOpzione,
  onEliminaOpzione,
  compatta,
  larghezza,
}: SelettoreCercabileProps) {
  if (caricamento) {
    return <Skeleton variant="rounded" height={compatta ? 36 : 54} sx={{ bgcolor: 'rgba(255,255,255,0.06)', width: compatta ? (larghezza ?? 220) : '100%' }} />
  }

  const campo = (
    <Autocomplete
      size="small"
      options={opzioni}
      getOptionLabel={(o) => o.nome}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      // MUI decide se un Autocomplete è controllato o no al primo render, guardando se `value` è
      // `undefined` — se lo è (es. strutturaId ancora null appena finito il caricamento, prima che
      // l'auto-selezione lo imposti un attimo dopo), l'Autocomplete si blocca in modalità "non
      // controllato" e ignora per sempre i cambi successivi della prop, restando vuoto anche quando
      // lo stato dell'app diventa corretto (bug reale segnalato: struttura auto-selezionata e dati
      // giusti mostrati, ma la select restava vuota). `null` invece resta sempre "controllato" — il
      // cast serve solo perché i tipi di MUI vietano `null` quando disableClearable è true, anche
      // se a runtime è l'unico modo corretto di rappresentare "nessuna selezione ancora" qui.
      value={opzioni.find((o) => o.id === valore) ?? (null as unknown as Opzione)}
      onChange={(_, v) => v && onChange(v.id)}
      disabled={opzioni.length === 0}
      disableClearable
      noOptionsText="Nessun risultato"
      sx={{
        width: compatta ? (larghezza ?? 220) : '100%',
        '& .MuiOutlinedInput-root': {
          bgcolor: '#20262F',
          borderRadius: '10px',
          ...(compatta ? { py: '0px !important', minHeight: 32 } : {}),
          '& fieldset': { borderColor: '#2C333D' },
          '&:hover fieldset': { borderColor: '#2C333D' },
          '&.Mui-focused fieldset': { borderColor: tokens.blue600 },
        },
        '& .MuiInputBase-input': { color: '#fff', fontWeight: 700, fontSize: 12.5 },
        '& .MuiInputLabel-root': { color: '#7E899A' },
        '& .MuiInputLabel-root.Mui-focused': { color: tokens.blue400 },
        '& .MuiSvgIcon-root': { color: '#7E899A' },
      }}
      slotProps={{
        paper: {
          sx: {
            bgcolor: '#20262F',
            color: '#fff',
            border: '1px solid #2C333D',
            '& .MuiAutocomplete-option': { fontSize: 13 },
            '& .MuiAutocomplete-option.Mui-focused': { bgcolor: '#2A3745' },
            '& .MuiAutocomplete-noOptions': { color: '#7E899A' },
          },
        },
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          placeholder={opzioni.length === 0 ? 'Nessuna opzione disponibile' : compatta ? `Cerca ${etichetta.toLowerCase()}…` : 'Cerca…'}
        />
      )}
      renderOption={(props, option) => {
        const { key, ...rest } = props
        return (
          <Box component="li" key={key} {...rest} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{option.nome}</Box>
            {onModificaOpzione && (
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation()
                  onModificaOpzione(option)
                }}
                sx={{ color: '#fff', '&:hover': { bgcolor: 'rgba(255,255,255,0.08)' } }}
              >
                <EditOutlinedIcon sx={{ fontSize: 16 }} />
              </IconButton>
            )}
            {onEliminaOpzione && (
              <IconButton
                size="small"
                onClick={(e) => {
                  e.stopPropagation()
                  onEliminaOpzione(option)
                }}
                sx={{ color: '#fff', '&:hover': { bgcolor: 'rgba(255,255,255,0.08)' } }}
              >
                <DeleteOutlineIcon sx={{ fontSize: 16 }} />
              </IconButton>
            )}
          </Box>
        )
      }}
    />
  )

  if (compatta) {
    return campo
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', px: 0.25 }}>
        <Typography sx={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '.07em', color: '#7E899A', textTransform: 'uppercase' }}>
          {etichetta}
        </Typography>
        {onAggiungi && (
          <Tooltip title="Aggiungi nuova struttura">
            <IconButton size="small" onClick={onAggiungi} sx={{ color: '#fff', p: 0.25, '&:hover': { bgcolor: 'rgba(255,255,255,0.08)' } }}>
              <AddIcon sx={{ fontSize: 15 }} />
            </IconButton>
          </Tooltip>
        )}
      </Box>
      {campo}
    </Box>
  )
}
