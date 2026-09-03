import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
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
import ApartmentOutlinedIcon from '@mui/icons-material/ApartmentOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteForeverIcon from '@mui/icons-material/DeleteForeverOutlined'
import { ApiError } from '../api/client'
import {
  GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA,
  giorniDaDisattivazione,
  useAggiornaServiziStruttura,
  useAggiornaUtente,
  useDashboardSuperAdmin,
  useEliminaStrutturaDefinitivamente,
  useImpostaAttivoCliente,
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
import { useToast } from '../toast/ToastContext'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

interface EsitoServizio {
  attivo: boolean
  errore?: string | null
  dettaglio?: string
}

const SERVIZI: {
  chiave: keyof Pick<StrutturaAdminDto, 'wubookAbilitato' | 'alloggiatiWebAbilitato' | 'osservatorioAbilitato' | 'payTouristAbilitato'>
  etichetta: string
  // Nome dell'esito mostrato sotto lo switch — non sempre coincide con l'etichetta della
  // concessione (es. si concede "Alloggiati Web" ma l'esito è l'invio "Polizia di Stato").
  statoNome: string
  // Esito dell'integrazione lato Cliente (es. "Alloggiati Web" attivata nelle sue Impostazioni) —
  // mostrato sotto lo switch di concessione, non un'informazione separata: uno switch "concesso"
  // senza sapere se il Cliente lo ha poi davvero attivato dice poco da solo.
  stato: (s: StrutturaAdminDto) => EsitoServizio
}[] = [
  {
    chiave: 'wubookAbilitato',
    etichetta: 'Wubook',
    statoNome: 'Wubook/licenza',
    stato: (s) => ({
      attivo: s.wubookAttivo,
      errore: s.wubookUltimoErrore,
      dettaglio: s.wubookCacheAggiornataAtUtc ? `agg. ${formattatoreDataOra.format(new Date(s.wubookCacheAggiornataAtUtc))}` : undefined,
    }),
  },
  { chiave: 'alloggiatiWebAbilitato', etichetta: 'Alloggiati Web', statoNome: 'Polizia di Stato', stato: (s) => ({ attivo: s.poliziaStatoAttiva }) },
  { chiave: 'osservatorioAbilitato', etichetta: 'Osservatorio', statoNome: 'Osservatorio', stato: (s) => ({ attivo: s.osservatorioAttivo }) },
  { chiave: 'payTouristAbilitato', etichetta: 'PayTourist', statoNome: 'PayTourist', stato: (s) => ({ attivo: s.payTouristAttivo }) },
]

export function SuperAdminDashboardPage() {
  const { isSuperAdmin } = useStruttura()
  const dashboard = useDashboardSuperAdmin(isSuperAdmin)

  // Solo l'Id, non l'oggetto Cliente intero: dopo ogni switch (Attiva/riattiva struttura, Servizi
  // concessi...) la dashboard viene invalidata e rifetchata — se il dialogo tenesse in state
  // l'oggetto Cliente "fotografato" al click, resterebbe con i vecchi valori finché non lo si
  // richiude e riapre (bug reale riscontrato: lo switch "Riattiva" sembrava non fare nulla).
  const [clienteStruttureAperto, setClienteStruttureAperto] = useState<string | null>(null)
  const [clienteInModifica, setClienteInModifica] = useState<ClienteAdminDto | null>(null)
  const [nuovoClienteAperto, setNuovoClienteAperto] = useState(false)
  const [strutturaDaEliminare, setStrutturaDaEliminare] = useState<{ struttura: StrutturaAdminDto; clienteRagioneSociale: string } | null>(null)

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
  const clienteConStruttureAperte = clienti.find((c) => c.id === clienteStruttureAperto) ?? null

  // Un Cliente può avere più utenti (uno per struttura, gestiti poi dal Cliente stesso in Utenti) —
  // qui interessa solo il primo, quello creato insieme al Cliente da "+ Nuovo Cliente".
  function trovaAdminCliente(clienteId: string): UtenteAdminDto | null {
    const utentiCliente = utenti.filter((u) => u.clienteId === clienteId)
    if (utentiCliente.length === 0) return null
    return utentiCliente.reduce((piuVecchio, u) => (new Date(u.createdAtUtc) < new Date(piuVecchio.createdAtUtc) ? u : piuVecchio))
  }

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
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 2 }}>
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
          <Box sx={{ overflowX: 'auto' }}>
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
        </Box>
      )}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Clienti</Typography>
          <Button variant="contained" color="primary" size="small" onClick={() => setNuovoClienteAperto(true)}>
            + Nuovo Cliente
          </Button>
        </Box>

        {clienti.length === 0 ? (
          <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 4, textAlign: 'center', color: tokens.textSecondary }}>
            Nessun Cliente presente.
          </Box>
        ) : (
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 2 }}>
            {clienti.map((c) => (
              <ClienteCard
                key={c.id}
                cliente={c}
                adminUtente={trovaAdminCliente(c.id)}
                onVediStrutture={() => setClienteStruttureAperto(c.id)}
                onModifica={() => setClienteInModifica(c)}
              />
            ))}
          </Box>
        )}
      </Box>

      {clienteConStruttureAperte && (
        <StruttureClienteDialog cliente={clienteConStruttureAperte} onClose={() => setClienteStruttureAperto(null)} />
      )}
      {clienteInModifica && (
        <ModificaClienteDialog
          cliente={clienteInModifica}
          adminUtente={trovaAdminCliente(clienteInModifica.id)}
          onClose={() => setClienteInModifica(null)}
        />
      )}
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
  const toast = useToast()

  function cambiaServizio(chiave: (typeof SERVIZI)[number]['chiave'], valore: boolean) {
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
      { onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  function cambiaAttivo(attivo: boolean) {
    impostaAttivo.mutate(
      { strutturaId: struttura.id, attivo },
      {
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] }),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.'),
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
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 160 }}>{struttura.nome}</Typography>

        <Tooltip title={struttura.attivo ? 'Elimina struttura (soft-delete)' : 'Riattiva struttura'}>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, minWidth: 90 }}>
            <Switch size="small" checked={struttura.attivo} onChange={(e) => cambiaAttivo(e.target.checked)} disabled={impostaAttivo.isPending} />
            <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: struttura.attivo ? tokens.ok600 : tokens.error600 }}>
              {struttura.attivo ? 'Attiva' : 'Eliminata'}
            </Typography>
          </Box>
        </Tooltip>
      </Box>

      <Box>
        <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em', mb: 0.75 }}>
          Servizi concessi
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 2.5, flexWrap: 'wrap' }}>
          {SERVIZI.map((s) => {
            const esito = s.stato(struttura)
            return (
              <Box key={s.chiave} sx={{ display: 'flex', flexDirection: 'column', gap: 0.75, minWidth: 170 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  <Switch
                    size="small"
                    checked={struttura[s.chiave]}
                    onChange={(e) => cambiaServizio(s.chiave, e.target.checked)}
                    disabled={aggiornaServizi.isPending}
                  />
                  <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{s.etichetta}</Typography>
                </Box>
                <EsitoServizioBadge nome={s.statoNome} {...esito} />
              </Box>
            )
          })}
        </Box>
      </Box>
    </Box>
  )
}

function EsitoServizioBadge({ nome, attivo, errore, dettaglio }: EsitoServizio & { nome: string }) {
  const inErrore = attivo && !!errore
  const colore = inErrore ? tokens.error600 : attivo ? tokens.ok600 : tokens.textTertiary

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, pl: '2px' }}>
      <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: colore, flex: '0 0 auto' }} />
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: 11.5, fontWeight: 600, color: tokens.textPrimary }}>{nome}</Typography>
        <Typography noWrap sx={{ fontSize: 11, color: inErrore ? tokens.error600 : tokens.textTertiary }}>
          {inErrore ? errore : attivo ? (dettaglio ?? 'attivo') : 'non attivo'}
        </Typography>
      </Box>
    </Box>
  )
}

function ClienteCard({
  cliente,
  adminUtente,
  onVediStrutture,
  onModifica,
}: {
  cliente: ClienteAdminDto
  adminUtente: UtenteAdminDto | null
  onVediStrutture: () => void
  onModifica: () => void
}) {
  const impostaAttivoCliente = useImpostaAttivoCliente()
  const toast = useToast()

  function cambiaAttivo(attivo: boolean) {
    impostaAttivoCliente.mutate(
      { clienteId: cliente.id, attivo },
      { onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  return (
    <Box
      sx={{
        border: `1px solid ${tokens.surfaceBorder}`,
        borderRadius: 2,
        bgcolor: tokens.surface,
        p: 2.25,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        minWidth: 0,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 1 }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15, wordBreak: 'break-word' }}>{cliente.ragioneSociale}</Typography>
          {adminUtente && (adminUtente.nome || adminUtente.cognome) && (
            <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              {[adminUtente.nome, adminUtente.cognome].filter(Boolean).join(' ')}
            </Typography>
          )}
          <Typography sx={{ fontSize: 12, color: tokens.textSecondary, fontFamily: fontMono, mt: 0.25 }}>
            {cliente.partitaIva ?? 'P.IVA non indicata'}
          </Typography>
          {adminUtente && (
            <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              {adminUtente.email}
            </Typography>
          )}
        </Box>
        <Box sx={{ display: 'flex', gap: 0.25, flex: '0 0 auto' }}>
          <Tooltip title="Strutture del Cliente">
            <IconButton size="small" onClick={onVediStrutture} disabled={cliente.strutture.length === 0}>
              <ApartmentOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Modifica dati Cliente e amministratore">
            <IconButton size="small" onClick={onModifica}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1.5 }}>
        <Tooltip title={cliente.attivo ? 'Sospendi il Cliente (nessun suo utente potrà più accedere)' : 'Riattiva il Cliente'}>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
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
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
          Creato il {formattatoreData.format(new Date(cliente.createdAtUtc))}
        </Typography>
      </Box>

      <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>
        {cliente.strutture.length === 0 ? 'Nessuna struttura' : `${cliente.strutture.length} ${cliente.strutture.length === 1 ? 'struttura' : 'strutture'}`}
      </Typography>
    </Box>
  )
}

function StruttureClienteDialog({ cliente, onClose }: { cliente: ClienteAdminDto; onClose: () => void }) {
  const [strutturaId, setStrutturaId] = useState(cliente.strutture[0]?.id ?? '')
  const struttura = cliente.strutture.find((s) => s.id === strutturaId) ?? null
  const [daEliminare, setDaEliminare] = useState(false)

  // Stessa soglia della tabella "Strutture eliminabili" in dashboard — qui è comodo poterla
  // eliminare subito dalla struttura che si sta già guardando, senza dover tornare indietro e
  // ricercarla in quella tabella.
  const eliminabile =
    !!struttura && !struttura.attivo && !!struttura.disattivataAtUtc && giorniDaDisattivazione(struttura.disattivataAtUtc) >= GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Strutture di {cliente.ragioneSociale}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {/* Il primo figlio letterale di questo contenitore flex non deve mai essere il campo con
            floating label sottostante: senza qualcosa che lo preceda, MUI ne taglia a metà la label
            (bug di rendering noto, vedi altri dialog dell'app con lo stesso pattern). */}
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>Seleziona la struttura da gestire.</Typography>
        <TextField select label="Struttura" value={strutturaId} onChange={(e) => setStrutturaId(e.target.value)} fullWidth>
          {cliente.strutture.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.nome}
            </MenuItem>
          ))}
        </TextField>
        {struttura && <RigaStruttura struttura={struttura} />}
        {eliminabile && (
          <Alert
            severity="warning"
            action={
              <Button color="error" size="small" onClick={() => setDaEliminare(true)}>
                Elimina definitivamente
              </Button>
            }
          >
            Disattivata da {giorniDaDisattivazione(struttura!.disattivataAtUtc!)} giorni: può essere eliminata in modo definitivo.
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose}>Chiudi</Button>
      </DialogActions>

      {daEliminare && struttura && (
        <EliminaStrutturaDialog
          struttura={struttura}
          clienteRagioneSociale={cliente.ragioneSociale}
          onClose={() => setDaEliminare(false)}
          onEliminata={() => {
            setDaEliminare(false)
            // La struttura appena eliminata non esiste più: l'elenco di questo dialogo (cliente.strutture)
            // è uno snapshot passato dal genitore e non si aggiorna da solo — si chiude tutto, il
            // genitore ha già invalidato la query e mostrerà i dati aggiornati alla riapertura.
            onClose()
          }}
        />
      )}
    </Dialog>
  )
}

function ModificaClienteDialog({
  cliente,
  adminUtente,
  onClose,
}: {
  cliente: ClienteAdminDto
  adminUtente: UtenteAdminDto | null
  onClose: () => void
}) {
  const [ragioneSociale, setRagioneSociale] = useState(cliente.ragioneSociale)
  const [partitaIva, setPartitaIva] = useState(cliente.partitaIva ?? '')
  const [email, setEmail] = useState(adminUtente?.email ?? '')
  const [nome, setNome] = useState(adminUtente?.nome ?? '')
  const [cognome, setCognome] = useState(adminUtente?.cognome ?? '')
  const [errore, setErrore] = useState<string | null>(null)

  const [nuovaPassword, setNuovaPassword] = useState('')
  const toast = useToast()

  const aggiorna = useAggiornaCliente()
  const aggiornaUtente = useAggiornaUtente()
  const resetPassword = useResettaPasswordUtente()
  const queryClient = useQueryClient()

  const inCorso = aggiorna.isPending || aggiornaUtente.isPending

  async function salva() {
    if (ragioneSociale.trim() === '') {
      setErrore('La ragione sociale è obbligatoria.')
      return
    }
    if (adminUtente && email.trim() === '') {
      setErrore('Email obbligatoria.')
      return
    }
    setErrore(null)
    try {
      await aggiorna.mutateAsync({ clienteId: cliente.id, request: { ragioneSociale: ragioneSociale.trim(), partitaIva: partitaIva.trim() || null } })
      if (adminUtente) {
        await aggiornaUtente.mutateAsync({
          utenteId: adminUtente.id,
          request: { email: email.trim(), nome: nome.trim() || null, cognome: cognome.trim() || null },
        })
      }
      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
      onClose()
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    }
  }

  function reimpostaPassword() {
    if (!adminUtente) return
    if (nuovaPassword.trim().length < 8) {
      toast.errore('La password deve avere almeno 8 caratteri.')
      return
    }
    resetPassword.mutate(
      { utenteId: adminUtente.id, nuovaPassword: nuovaPassword.trim() },
      {
        onSuccess: () => {
          toast.successo('Password reimpostata.')
          setNuovaPassword('')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
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
          disabled={inCorso}
          autoFocus
        />
        <TextField label="P.IVA" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={inCorso} />

        {adminUtente ? (
          <>
            <Divider />
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
              Utente amministratore
            </Typography>
            <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={inCorso} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
              <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
            </Box>

            <Divider />
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Reimposta la password di questo utente (non serve conoscere quella attuale).
            </Typography>
            <Box sx={{ display: 'flex', gap: 2, alignItems: 'flex-start' }}>
              <TextField
                label="Nuova password"
                type="password"
                value={nuovaPassword}
                onChange={(e) => setNuovaPassword(e.target.value)}
                fullWidth
                disabled={resetPassword.isPending}
              />
              <Button
                variant="outlined"
                onClick={reimpostaPassword}
                disabled={resetPassword.isPending || nuovaPassword.trim() === ''}
                sx={{ whiteSpace: 'nowrap', flex: '0 0 auto' }}
              >
                Reimposta
              </Button>
            </Box>
          </>
        ) : (
          <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>Nessun utente amministratore trovato per questo Cliente.</Typography>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
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
        <Button variant="contained" color="primary" onClick={salva} disabled={salvataggioInCorso}>
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
  onEliminata,
}: {
  struttura: StrutturaAdminDto
  clienteRagioneSociale: string
  onClose: () => void
  /** Solo quando l'eliminazione va davvero a buon fine — distinto da onClose, chiamato anche su Annulla. */
  onEliminata?: () => void
}) {
  const [conferma, setConferma] = useState('')
  const toast = useToast()
  const elimina = useEliminaStrutturaDefinitivamente()

  const confermaValida = conferma.trim() === struttura.nome

  function procedi() {
    if (!confermaValida) return
    elimina.mutate(struttura.id, {
      onSuccess: () => (onEliminata ?? onClose)(),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ color: tokens.error600 }}>Elimina definitivamente "{struttura.nome}"</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
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
