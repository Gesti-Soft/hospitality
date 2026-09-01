import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Collapse from '@mui/material/Collapse'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
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
import DeleteForeverIcon from '@mui/icons-material/DeleteForeverOutlined'
import { ApiError } from '../api/client'
import {
  GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA,
  giorniDaDisattivazione,
  useAggiornaServiziStruttura,
  useDashboardSuperAdmin,
  useEliminaStrutturaDefinitivamente,
  useImpostaAttivoCliente,
  type ClienteAdminDto,
  type StrutturaAdminDto,
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
  const [clienteInModifica, setClienteInModifica] = useState<ClienteAdminDto | null>(null)
  const [nuovoClienteAperto, setNuovoClienteAperto] = useState(false)
  const [strutturaDaEliminare, setStrutturaDaEliminare] = useState<{ struttura: StrutturaAdminDto; clienteRagioneSociale: string } | null>(null)

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

  const clientiAttivi = clienti.filter((c) => c.attivo).length
  const struttureTotali = clienti.reduce((tot, c) => tot + c.strutture.length, 0)
  const struttureConErroreLicenza = clienti.reduce(
    (tot, c) => tot + c.strutture.filter((s) => s.wubookAttivo && s.wubookUltimoErrore).length,
    0,
  )

  const struttureEliminabili = clienti.flatMap((c) =>
    c.strutture
      .filter((s) => !s.attivo && s.disattivataAtUtc && giorniDaDisattivazione(s.disattivataAtUtc) >= GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA)
      .map((s) => ({ struttura: s, clienteRagioneSociale: c.ragioneSociale })),
  )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 2 }}>
        <KpiCard etichetta="Clienti" valore={String(clienti.length)} dettaglio={`${clientiAttivi} attivi`} />
        <KpiCard etichetta="Strutture" valore={String(struttureTotali)} />
        <KpiCard
          etichetta="Errori di licenza Wubook"
          valore={String(struttureConErroreLicenza)}
          dettaglio="strutture da controllare"
          accento={struttureConErroreLicenza > 0}
        />
      </Box>

      {struttureEliminabili.length > 0 && (
        <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.error600}`, borderRadius: 2, overflow: 'hidden' }}>
          <Box sx={{ p: '18px 20px', borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Strutture eliminabili</Typography>
            <Typography sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              Disattivate da almeno {GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA} giorni — l'eliminazione è definitiva e cancella tutti i dati collegati
              (camere, prenotazioni, ospiti, fatture...). Nessuna cancellazione automatica: va confermata singolarmente.
            </Typography>
          </Box>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Cliente</TableCell>
                <TableCell>Struttura</TableCell>
                <TableCell>Disattivata il</TableCell>
                <TableCell align="right">Giorni</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {struttureEliminabili.map(({ struttura, clienteRagioneSociale }) => (
                <TableRow key={struttura.id} hover>
                  <TableCell sx={{ color: tokens.textSecondary }}>{clienteRagioneSociale}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{struttura.nome}</TableCell>
                  <TableCell sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
                    {struttura.disattivataAtUtc ? formattatoreData.format(new Date(struttura.disattivataAtUtc)) : '—'}
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {struttura.disattivataAtUtc ? giorniDaDisattivazione(struttura.disattivataAtUtc) : '—'}
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Elimina definitivamente">
                      <IconButton size="small" color="error" onClick={() => setStrutturaDaEliminare({ struttura, clienteRagioneSociale })}>
                        <DeleteForeverIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, overflow: 'hidden' }}>
        <Box sx={{ p: '18px 20px', borderBottom: `1px solid ${tokens.surfaceBorder}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Clienti</Typography>
          <Button variant="contained" color="secondary" size="small" onClick={() => setNuovoClienteAperto(true)}>
            + Nuovo Cliente
          </Button>
        </Box>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell width={40} />
              <TableCell>Ragione sociale</TableCell>
              <TableCell>P.IVA</TableCell>
              <TableCell align="right">Strutture</TableCell>
              <TableCell align="center">Stato</TableCell>
              <TableCell>Creato il</TableCell>
              <TableCell align="right">Azioni</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {clienti.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                  Nessun Cliente presente.
                </TableCell>
              </TableRow>
            )}
            {clienti.map((c) => (
              <RigaCliente
                key={c.id}
                cliente={c}
                espanso={espansi.has(c.id)}
                onToggle={() => toggleEspanso(c.id)}
                onModifica={() => setClienteInModifica(c)}
              />
            ))}
          </TableBody>
        </Table>
      </Box>

      {clienteInModifica && <ModificaClienteDialog cliente={clienteInModifica} onClose={() => setClienteInModifica(null)} />}
      {nuovoClienteAperto && <NuovoClienteDialog onClose={() => setNuovoClienteAperto(false)} />}
      {strutturaDaEliminare && (
        <EliminaStrutturaDialog
          struttura={strutturaDaEliminare.struttura}
          clienteRagioneSociale={strutturaDaEliminare.clienteRagioneSociale}
          onClose={() => setStrutturaDaEliminare(null)}
        />
      )}
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

function RigaCliente({
  cliente,
  espanso,
  onToggle,
  onModifica,
}: {
  cliente: ClienteAdminDto
  espanso: boolean
  onToggle: () => void
  onModifica: () => void
}) {
  const impostaAttivoCliente = useImpostaAttivoCliente()
  const [errore, setErrore] = useState<string | null>(null)

  function cambiaAttivo(attivo: boolean) {
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
          {cliente.strutture.length > 0 && (
            <IconButton size="small" onClick={onToggle}>
              {espanso ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
            </IconButton>
          )}
        </TableCell>
        <TableCell sx={{ fontWeight: 600 }}>{cliente.ragioneSociale}</TableCell>
        <TableCell sx={{ color: tokens.textSecondary, fontFamily: fontMono }}>{cliente.partitaIva ?? '—'}</TableCell>
        <TableCell align="right" sx={{ fontFamily: fontMono }}>
          {cliente.strutture.length}
        </TableCell>
        <TableCell align="center">
          <Tooltip title={cliente.attivo ? 'Sospendi il Cliente (nessun suo utente potrà più accedere)' : 'Riattiva il Cliente'}>
            <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
              <Switch
                size="small"
                checked={cliente.attivo}
                onChange={(e) => cambiaAttivo(e.target.checked)}
                disabled={impostaAttivoCliente.isPending}
              />
              <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: cliente.attivo ? tokens.ok600 : tokens.error600 }}>
                {cliente.attivo ? 'Attivo' : 'Sospeso'}
              </Typography>
            </Box>
          </Tooltip>
        </TableCell>
        <TableCell sx={{ fontSize: 12.5, color: tokens.textSecondary }}>{formattatoreData.format(new Date(cliente.createdAtUtc))}</TableCell>
        <TableCell align="right">
          <Tooltip title="Modifica dati Cliente (ragione sociale, P.IVA)">
            <IconButton size="small" onClick={onModifica}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </TableCell>
      </TableRow>
      {cliente.strutture.length > 0 && (
        <TableRow>
          <TableCell colSpan={7} sx={{ p: 0, border: espanso ? undefined : 'none' }}>
            <Collapse in={espanso} unmountOnExit>
              <Box sx={{ p: '10px 20px 18px 56px', bgcolor: tokens.paper, display: 'flex', flexDirection: 'column', gap: 1 }}>
                {errore && <Alert severity="error">{errore}</Alert>}
                <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
                  Strutture
                </Typography>
                {cliente.strutture.map((s) => (
                  <RigaStruttura key={s.id} struttura={s} />
                ))}
              </Box>
            </Collapse>
          </TableCell>
        </TableRow>
      )}
    </>
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
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>
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

function NuovoClienteDialog({ onClose }: { onClose: () => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [nome, setNome] = useState('')
  const [cognome, setCognome] = useState('')
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
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
    if (!isSuperAdmin && ragioneSociale.trim() === '') {
      setErrore('Indica la ragione sociale del Cliente.')
      return
    }
    setErrore(null)
    setSalvataggioInCorso(true)
    try {
      // Il Super Admin crea solo il Cliente e il suo primo utente, che sarà automaticamente
      // amministratore delle strutture che andrà a creare — la gestione degli utenti per singola
      // struttura (colleghi, ruoli) resta poi al Cliente stesso, non riguarda più il Super Admin.
      let clienteIdFinale: string | null = null
      if (!isSuperAdmin) {
        clienteIdFinale = (await creaCliente.mutateAsync({ ragioneSociale: ragioneSociale.trim(), partitaIva: partitaIva.trim() || null })).id
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
      <DialogTitle>Nuovo Cliente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Crea il Cliente e le credenziali del suo primo utente, che sarà amministratore delle strutture che creerà.
        </Typography>
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
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField label="Ragione sociale" value={ragioneSociale} onChange={(e) => setRagioneSociale(e.target.value)} fullWidth disabled={salvataggioInCorso} />
            <TextField label="P.IVA (opzionale)" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={salvataggioInCorso} />
          </Box>
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

function EliminaStrutturaDialog({
  struttura,
  clienteRagioneSociale,
  onClose,
}: {
  struttura: StrutturaAdminDto
  clienteRagioneSociale: string
  onClose: () => void
}) {
  const [conferma, setConferma] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const elimina = useEliminaStrutturaDefinitivamente()

  const confermaValida = conferma.trim() === struttura.nome

  function procedi() {
    if (!confermaValida) return
    setErrore(null)
    elimina.mutate(struttura.id, {
      onSuccess: onClose,
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ color: tokens.error600 }}>Elimina definitivamente "{struttura.nome}"</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Alert severity="error">
          Questa azione è <strong>irreversibile</strong>: cancella per sempre la struttura "{struttura.nome}" del Cliente "{clienteRagioneSociale}"
          e tutti i dati collegati (camere, prenotazioni, ospiti, fatture, integrazioni). Non è un semplice disattiva/riattiva.
        </Alert>
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Per confermare, scrivi il nome esatto della struttura: <strong>{struttura.nome}</strong>
        </Typography>
        <TextField
          value={conferma}
          onChange={(e) => setConferma(e.target.value)}
          fullWidth
          disabled={elimina.isPending}
          autoFocus
          placeholder={struttura.nome}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={elimina.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="error" onClick={procedi} disabled={!confermaValida || elimina.isPending}>
          Elimina definitivamente
        </Button>
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
