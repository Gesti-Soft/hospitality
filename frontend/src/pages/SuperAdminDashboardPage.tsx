import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Collapse from '@mui/material/Collapse'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Switch from '@mui/material/Switch'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import EditIcon from '@mui/icons-material/EditOutlined'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import LockResetIcon from '@mui/icons-material/LockResetOutlined'
import { ApiError } from '../api/client'
import {
  useAggiornaServiziStruttura,
  useAggiornaUtente,
  useDashboardSuperAdmin,
  useImpostaAttivoCliente,
  useImpostaAttivoUtente,
  useResettaPasswordUtente,
  type ClienteAdminDto,
  type StrutturaAdminDto,
  type UtenteAdminDto,
} from '../api/superAdmin'
import { useAggiornaCliente, useCreaCliente } from '../api/clienti'
import { useImpostaAttivoStruttura } from '../api/strutture'
import { useCreaUtente } from '../api/utenti'
import { useStruttura } from '../struttura/StrutturaContext'
import { fontDisplay, fontMono, tokens } from '../theme'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

const SERVIZI: { chiave: keyof Pick<StrutturaAdminDto, 'wubookAbilitato' | 'alloggiatiWebAbilitato' | 'osservatorioAbilitato' | 'payTouristAbilitato'>; etichetta: string }[] = [
  { chiave: 'wubookAbilitato', etichetta: 'Wubook' },
  { chiave: 'alloggiatiWebAbilitato', etichetta: 'Alloggiati Web' },
  { chiave: 'osservatorioAbilitato', etichetta: 'Osservatorio' },
  { chiave: 'payTouristAbilitato', etichetta: 'PayTourist' },
]

export function SuperAdminDashboardPage() {
  const { isSuperAdmin } = useStruttura()
  const dashboard = useDashboardSuperAdmin(isSuperAdmin)

  const [espansi, setEspansi] = useState<Set<string>>(new Set())
  const [utenteResetPassword, setUtenteResetPassword] = useState<UtenteAdminDto | null>(null)
  const [utenteInModifica, setUtenteInModifica] = useState<UtenteAdminDto | null>(null)
  const [clienteInModifica, setClienteInModifica] = useState<ClienteAdminDto | null>(null)
  const [nuovoUtenteAperto, setNuovoUtenteAperto] = useState(false)

  function toggleEspanso(clienteId: string) {
    setEspansi((prec) => {
      const next = new Set(prec)
      if (next.has(clienteId)) next.delete(clienteId)
      else next.add(clienteId)
      return next
    })
  }

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  if (dashboard.isLoading) {
    return <Skeleton variant="rounded" height={480} />
  }

  if (dashboard.isError) {
    return <Alert severity="error">Impossibile caricare la dashboard. Riprova.</Alert>
  }

  const clienti = dashboard.data?.clienti ?? []
  const utenti = dashboard.data?.utenti ?? []

  const clientiAttivi = clienti.filter((c) => c.attivo).length
  const struttureTotali = clienti.reduce((tot, c) => tot + c.strutture.length, 0)
  const utentiAttivi = utenti.filter((u) => u.attivo).length
  const struttureConErroreLicenza = clienti.reduce(
    (tot, c) => tot + c.strutture.filter((s) => s.wubookAttivo && s.wubookUltimoErrore).length,
    0,
  )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 2 }}>
        <KpiCard etichetta="Clienti" valore={String(clienti.length)} dettaglio={`${clientiAttivi} attivi`} />
        <KpiCard etichetta="Strutture" valore={String(struttureTotali)} />
        <KpiCard etichetta="Utenti" valore={String(utenti.length)} dettaglio={`${utentiAttivi} attivi`} />
        <KpiCard
          etichetta="Errori di licenza Wubook"
          valore={String(struttureConErroreLicenza)}
          dettaglio="strutture da controllare"
          accento={struttureConErroreLicenza > 0}
        />
      </Box>

      <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, overflow: 'hidden' }}>
        <Box sx={{ p: '18px 20px', borderBottom: `1px solid ${tokens.surfaceBorder}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Utenti</Typography>
          <Button variant="contained" color="secondary" size="small" onClick={() => setNuovoUtenteAperto(true)}>
            + Nuovo utente
          </Button>
        </Box>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell width={40} />
              <TableCell>Email</TableCell>
              <TableCell>Nome</TableCell>
              <TableCell>Cliente</TableCell>
              <TableCell>Ruolo</TableCell>
              <TableCell align="center">Stato</TableCell>
              <TableCell>Creato il</TableCell>
              <TableCell align="right">Azioni</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {utenti.length === 0 && (
              <TableRow>
                <TableCell colSpan={8} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                  Nessun utente presente.
                </TableCell>
              </TableRow>
            )}
            {utenti.map((u) => (
              <RigaUtente
                key={u.id}
                utente={u}
                cliente={clienti.find((c) => c.id === u.clienteId) ?? null}
                espanso={espansi.has(u.id)}
                onToggle={() => toggleEspanso(u.id)}
                onResettaPassword={() => setUtenteResetPassword(u)}
                onModifica={() => setUtenteInModifica(u)}
                onModificaCliente={(c) => setClienteInModifica(c)}
              />
            ))}
          </TableBody>
        </Table>
      </Box>

      {utenteResetPassword && <ResetPasswordDialog utente={utenteResetPassword} onClose={() => setUtenteResetPassword(null)} />}
      {utenteInModifica && <ModificaUtenteDialog utente={utenteInModifica} onClose={() => setUtenteInModifica(null)} />}
      {clienteInModifica && <ModificaClienteDialog cliente={clienteInModifica} onClose={() => setClienteInModifica(null)} />}
      {nuovoUtenteAperto && <NuovoUtenteDialog clienti={clienti} onClose={() => setNuovoUtenteAperto(false)} />}
    </Box>
  )
}

function RigaStruttura({ struttura }: { struttura: StrutturaAdminDto }) {
  const aggiornaServizi = useAggiornaServiziStruttura()
  const impostaAttivo = useImpostaAttivoStruttura()
  const queryClient = useQueryClient()
  const [errore, setErrore] = useState<string | null>(null)

  function cambiaServizio(chiave: (typeof SERVIZI)[number]['chiave'], valore: boolean) {
    setErrore(null)
    aggiornaServizi.mutate(
      {
        strutturaId: struttura.id,
        request: {
          wubookAbilitato: struttura.wubookAbilitato,
          alloggiatiWebAbilitato: struttura.alloggiatiWebAbilitato,
          osservatorioAbilitato: struttura.osservatorioAbilitato,
          payTouristAbilitato: struttura.payTouristAbilitato,
          [chiave]: valore,
        },
      },
      { onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  function cambiaAttivo(attivo: boolean) {
    setErrore(null)
    impostaAttivo.mutate(
      { strutturaId: struttura.id, attivo },
      {
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] }),
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita.'),
      },
    )
  }

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        gap: 1.25,
        p: '10px 14px',
        border: `1px solid ${struttura.attivo ? tokens.surfaceBorder : tokens.error600}`,
        borderRadius: 1.5,
        bgcolor: tokens.surface,
        opacity: struttura.attivo ? 1 : 0.7,
      }}
    >
      {errore && <Alert severity="error">{errore}</Alert>}

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 160 }}>{struttura.nome}</Typography>

        <Tooltip title={struttura.attivo ? 'Elimina struttura (soft-delete)' : 'Riattiva struttura'}>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, minWidth: 90 }}>
            <Switch size="small" checked={struttura.attivo} onChange={(e) => cambiaAttivo(e.target.checked)} disabled={impostaAttivo.isPending} />
            <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: struttura.attivo ? tokens.ok600 : tokens.error600 }}>
              {struttura.attivo ? 'Attiva' : 'Eliminata'}
            </Typography>
          </Box>
        </Tooltip>

        <StatoIntegrazione
          nome="Wubook/licenza"
          attivo={struttura.wubookAttivo}
          errore={struttura.wubookUltimoErrore}
          dettaglio={
            struttura.wubookCacheAggiornataAtUtc
              ? `agg. ${formattatoreDataOra.format(new Date(struttura.wubookCacheAggiornataAtUtc))}`
              : undefined
          }
        />
        <StatoIntegrazione nome="Polizia di Stato" attivo={struttura.poliziaStatoAttiva} />
        <StatoIntegrazione nome="Osservatorio" attivo={struttura.osservatorioAttivo} />
        <StatoIntegrazione nome="PayTourist" attivo={struttura.payTouristAttivo} />
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, pl: '160px', flexWrap: 'wrap' }}>
        <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
          Servizi concessi
        </Typography>
        {SERVIZI.map((s) => (
          <Box key={s.chiave} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <Switch
              size="small"
              checked={struttura[s.chiave]}
              onChange={(e) => cambiaServizio(s.chiave, e.target.checked)}
              disabled={aggiornaServizi.isPending}
            />
            <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{s.etichetta}</Typography>
          </Box>
        ))}
      </Box>
    </Box>
  )
}

function StatoIntegrazione({ nome, attivo, errore, dettaglio }: { nome: string; attivo: boolean; errore?: string | null; dettaglio?: string }) {
  const inErrore = attivo && !!errore
  const colore = inErrore ? tokens.error600 : attivo ? tokens.ok600 : tokens.textTertiary

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 170 }}>
      <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: colore, flex: '0 0 auto' }} />
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: tokens.textPrimary }}>{nome}</Typography>
        <Typography noWrap sx={{ fontSize: 11, color: inErrore ? tokens.error600 : tokens.textTertiary }}>
          {inErrore ? errore : attivo ? (dettaglio ?? 'attivo') : 'non attivo'}
        </Typography>
      </Box>
    </Box>
  )
}

function RigaUtente({
  utente,
  cliente,
  espanso,
  onToggle,
  onResettaPassword,
  onModifica,
  onModificaCliente,
}: {
  utente: UtenteAdminDto
  cliente: ClienteAdminDto | null
  espanso: boolean
  onToggle: () => void
  onResettaPassword: () => void
  onModifica: () => void
  onModificaCliente: (cliente: ClienteAdminDto) => void
}) {
  const nomeCompleto = [utente.nome, utente.cognome].filter(Boolean).join(' ')
  const impostaAttivoUtente = useImpostaAttivoUtente()
  const impostaAttivoCliente = useImpostaAttivoCliente()
  const [errore, setErrore] = useState<string | null>(null)

  function cambiaAttivo(attivo: boolean) {
    setErrore(null)
    impostaAttivoUtente.mutate(
      { utenteId: utente.id, attivo },
      { onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  function cambiaClienteAttivo(attivo: boolean) {
    if (!cliente) return
    setErrore(null)
    impostaAttivoCliente.mutate(
      { clienteId: cliente.id, attivo },
      { onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  return (
    <>
      <TableRow hover>
        <TableCell>
          {cliente && (
            <IconButton size="small" onClick={onToggle}>
              {espanso ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            </IconButton>
          )}
        </TableCell>
        <TableCell sx={{ fontWeight: 600 }}>{utente.email}</TableCell>
        <TableCell sx={{ color: tokens.textSecondary }}>{nomeCompleto || '—'}</TableCell>
        <TableCell sx={{ color: tokens.textSecondary }}>{utente.clienteRagioneSociale ?? '—'}</TableCell>
        <TableCell>
          {utente.isSuperAdmin ? (
            <Chip size="small" label="Super Admin" sx={{ bgcolor: tokens.orange100, color: tokens.orange700, fontWeight: 700 }} />
          ) : (
            <Chip size="small" label="Operatore" sx={{ bgcolor: tokens.blue100, color: tokens.blue700, fontWeight: 700 }} />
          )}
        </TableCell>
        <TableCell align="center">
          <Tooltip title={utente.attivo ? 'Disattiva utente' : 'Riattiva utente'}>
            <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
              <Switch
                size="small"
                checked={utente.attivo}
                onChange={(e) => cambiaAttivo(e.target.checked)}
                disabled={impostaAttivoUtente.isPending}
              />
              <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: utente.attivo ? tokens.ok600 : tokens.error600 }}>
                {utente.attivo ? 'Attivo' : 'Disattivo'}
              </Typography>
            </Box>
          </Tooltip>
        </TableCell>
        <TableCell sx={{ fontSize: 12.5, color: tokens.textSecondary }}>{formattatoreData.format(new Date(utente.createdAtUtc))}</TableCell>
        <TableCell align="right">
          <Tooltip title="Modifica utente">
            <IconButton size="small" onClick={onModifica}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Reimposta password (supporto/assistenza)">
            <IconButton size="small" onClick={onResettaPassword}>
              <LockResetIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </TableCell>
      </TableRow>
      {cliente && (
        <TableRow>
          <TableCell colSpan={8} sx={{ p: 0, border: espanso ? undefined : 'none' }}>
            <Collapse in={espanso} unmountOnExit>
              <Box sx={{ p: '10px 20px 18px 56px', bgcolor: tokens.paper, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
                {errore && <Alert severity="error">{errore}</Alert>}

                <Box sx={{ display: 'flex', alignItems: 'center', gap: 3, flexWrap: 'wrap' }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                    <Typography sx={{ fontSize: 12.5, fontWeight: 700 }}>{cliente.ragioneSociale}</Typography>
                    <Tooltip title="Modifica dati Cliente (ragione sociale, P.IVA)">
                      <IconButton size="small" onClick={() => onModificaCliente(cliente)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                  {cliente.partitaIva && (
                    <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
                      P.IVA <span style={{ fontFamily: fontMono }}>{cliente.partitaIva}</span>
                    </Typography>
                  )}
                  <Tooltip title={cliente.attivo ? 'Sospendi il Cliente (nessun suo utente potrà più accedere)' : 'Riattiva il Cliente'}>
                    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
                      <Switch
                        size="small"
                        checked={cliente.attivo}
                        onChange={(e) => cambiaClienteAttivo(e.target.checked)}
                        disabled={impostaAttivoCliente.isPending}
                      />
                      <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: cliente.attivo ? tokens.ok600 : tokens.error600 }}>
                        Cliente {cliente.attivo ? 'attivo' : 'sospeso'}
                      </Typography>
                    </Box>
                  </Tooltip>
                </Box>

                {cliente.strutture.length > 0 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                    <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
                      Strutture
                    </Typography>
                    {cliente.strutture.map((s) => (
                      <RigaStruttura key={s.id} struttura={s} />
                    ))}
                  </Box>
                )}
              </Box>
            </Collapse>
          </TableCell>
        </TableRow>
      )}
    </>
  )
}

function ModificaUtenteDialog({ utente, onClose }: { utente: UtenteAdminDto; onClose: () => void }) {
  const [email, setEmail] = useState(utente.email)
  const [nome, setNome] = useState(utente.nome ?? '')
  const [cognome, setCognome] = useState(utente.cognome ?? '')
  const [errore, setErrore] = useState<string | null>(null)

  const aggiorna = useAggiornaUtente()

  function salva() {
    if (email.trim() === '') {
      setErrore('L\'email è obbligatoria.')
      return
    }
    setErrore(null)
    aggiorna.mutate(
      { utenteId: utente.id, request: { email: email.trim(), nome: nome.trim() || null, cognome: cognome.trim() || null } },
      {
        onSuccess: onClose,
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Modifica utente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={aggiorna.isPending} autoFocus />
        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={aggiorna.isPending} />
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        </Box>
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

function ModificaClienteDialog({ cliente, onClose }: { cliente: ClienteAdminDto; onClose: () => void }) {
  const [ragioneSociale, setRagioneSociale] = useState(cliente.ragioneSociale)
  const [partitaIva, setPartitaIva] = useState(cliente.partitaIva ?? '')
  const [errore, setErrore] = useState<string | null>(null)

  const aggiorna = useAggiornaCliente()
  const queryClient = useQueryClient()

  function salva() {
    if (ragioneSociale.trim() === '') {
      setErrore('La ragione sociale è obbligatoria.')
      return
    }
    setErrore(null)
    aggiorna.mutate(
      { clienteId: cliente.id, request: { ragioneSociale: ragioneSociale.trim(), partitaIva: partitaIva.trim() || null } },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
          onClose()
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Modifica Cliente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <TextField
          label="Ragione sociale"
          value={ragioneSociale}
          onChange={(e) => setRagioneSociale(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          autoFocus
        />
        <TextField label="P.IVA" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={aggiorna.isPending} />
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

function NuovoUtenteDialog({ clienti, onClose }: { clienti: ClienteAdminDto[]; onClose: () => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [nome, setNome] = useState('')
  const [cognome, setCognome] = useState('')
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
  const [nuovoCliente, setNuovoCliente] = useState(clienti.length === 0)
  const [clienteId, setClienteId] = useState('')
  const [ragioneSociale, setRagioneSociale] = useState('')
  const [partitaIva, setPartitaIva] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvataggioInCorso, setSalvataggioInCorso] = useState(false)

  const creaCliente = useCreaCliente()
  const creaUtente = useCreaUtente()
  const queryClient = useQueryClient()

  async function salva() {
    if (email.trim() === '' || password.trim().length < 8) {
      setErrore('Email obbligatoria, password di almeno 8 caratteri.')
      return
    }
    if (!isSuperAdmin && !nuovoCliente && clienteId === '') {
      setErrore('Seleziona il Cliente a cui appartiene questo utente, oppure creane uno nuovo.')
      return
    }
    if (!isSuperAdmin && nuovoCliente && ragioneSociale.trim() === '') {
      setErrore('Indica la ragione sociale del nuovo Cliente.')
      return
    }
    setErrore(null)
    setSalvataggioInCorso(true)
    try {
      let clienteIdFinale: string | null = null
      if (!isSuperAdmin) {
        clienteIdFinale = nuovoCliente
          ? (await creaCliente.mutateAsync({ ragioneSociale: ragioneSociale.trim(), partitaIva: partitaIva.trim() || null })).id
          : clienteId
      }

      await creaUtente.mutateAsync({
        email: email.trim(),
        password: password.trim(),
        nome: nome.trim() || null,
        cognome: cognome.trim() || null,
        isSuperAdmin,
        clienteId: clienteIdFinale,
      })

      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
      onClose()
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    } finally {
      setSalvataggioInCorso(false)
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Nuovo utente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={salvataggioInCorso} autoFocus />
          <TextField label="Password" type="text" value={password} onChange={(e) => setPassword(e.target.value)} fullWidth disabled={salvataggioInCorso} />
        </Box>
        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={salvataggioInCorso} />
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={salvataggioInCorso} />
        </Box>
        <FormControlLabel
          control={<Checkbox checked={isSuperAdmin} onChange={(e) => setIsSuperAdmin(e.target.checked)} disabled={salvataggioInCorso} />}
          label="Super Admin (staff GestiSoft, non appartiene a un Cliente)"
        />
        {!isSuperAdmin && (
          <>
            <FormControlLabel
              control={<Checkbox checked={nuovoCliente} onChange={(e) => setNuovoCliente(e.target.checked)} disabled={salvataggioInCorso} />}
              label="Nuovo Cliente (crea anche l'azienda, non solo l'utente)"
            />
            {nuovoCliente ? (
              <Box sx={{ display: 'flex', gap: 2 }}>
                <TextField
                  label="Ragione sociale"
                  value={ragioneSociale}
                  onChange={(e) => setRagioneSociale(e.target.value)}
                  fullWidth
                  disabled={salvataggioInCorso}
                />
                <TextField label="P.IVA (opzionale)" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={salvataggioInCorso} />
              </Box>
            ) : (
              <TextField
                select
                label="Cliente"
                value={clienteId}
                onChange={(e) => setClienteId(e.target.value)}
                fullWidth
                disabled={salvataggioInCorso}
              >
                {clienti.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.ragioneSociale}
                  </MenuItem>
                ))}
              </TextField>
            )}
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={salvataggioInCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={salvataggioInCorso}>
          Crea
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function ResetPasswordDialog({ utente, onClose }: { utente: UtenteAdminDto; onClose: () => void }) {
  const [nuovaPassword, setNuovaPassword] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [fatto, setFatto] = useState(false)

  const resetta = useResettaPasswordUtente()

  function salva() {
    if (nuovaPassword.trim().length < 8) {
      setErrore('La password deve avere almeno 8 caratteri.')
      return
    }
    setErrore(null)
    resetta.mutate(
      { utenteId: utente.id, nuovaPassword: nuovaPassword.trim() },
      {
        onSuccess: () => setFatto(true),
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Reimposta password — {utente.email}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        {fatto ? (
          <Alert severity="success">Password aggiornata. Comunicala all'utente per un canale sicuro (telefono, non email).</Alert>
        ) : (
          <>
            <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
              Imposta una nuova password per conto dell'utente (supporto/assistenza) — non serve conoscere quella attuale.
            </Typography>
            <TextField
              label="Nuova password"
              type="text"
              value={nuovaPassword}
              onChange={(e) => setNuovaPassword(e.target.value)}
              fullWidth
              disabled={resetta.isPending}
              autoFocus
            />
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={resetta.isPending}>
          {fatto ? 'Chiudi' : 'Annulla'}
        </Button>
        {!fatto && (
          <Button variant="contained" color="secondary" onClick={salva} disabled={resetta.isPending}>
            Reimposta
          </Button>
        )}
      </DialogActions>
    </Dialog>
  )
}

function KpiCard({ etichetta, valore, dettaglio, accento }: { etichetta: string; valore: string; dettaglio?: string; accento?: boolean }) {
  return (
    <Box
      sx={{
        bgcolor: tokens.surface,
        border: `1px solid ${accento ? tokens.error600 : tokens.surfaceBorder}`,
        borderWidth: accento ? 1.5 : 1,
        borderRadius: 2,
        p: '20px 22px',
      }}
    >
      <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: accento ? tokens.error600 : tokens.textSecondary }}>{etichetta}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, mt: 1 }}>
        <Typography sx={{ fontFamily: fontMono, fontSize: 28, fontWeight: 600 }}>{valore}</Typography>
        {dettaglio && <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>{dettaglio}</Typography>}
      </Box>
    </Box>
  )
}
