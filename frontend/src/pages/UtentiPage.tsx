import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined'
import { useStruttura } from '../struttura/StrutturaContext'
import { useAuth } from '../auth/AuthContext'
import { ApiError } from '../api/client'
import {
  RuoloUtente,
  useAssegnazioniStruttura,
  useCambiaPasswordPropria,
  useRimuoviAssegnazione,
  useUtentiCliente,
  type AssegnazioneStrutturaDto,
} from '../api/utenti'
import { fontDisplay, fontMono, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { useMobile } from '../lib/useMobile'
import { AssegnaRuoloDialog, type StatoAssegnazioneIniziale } from '../components/AssegnaRuoloDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../components/CardElenco'

const ETICHETTA_RUOLO: Record<RuoloUtente, string> = {
  [RuoloUtente.Administrator]: 'Amministratore',
  [RuoloUtente.Receptionist]: 'Receptionist',
  [RuoloUtente.Housekeeper]: 'Addetto pulizie',
  [RuoloUtente.Manager]: 'Manager',
  [RuoloUtente.Accountant]: 'Contabile',
  [RuoloUtente.Maintenance]: 'Manutenzione',
  [RuoloUtente.FnbManager]: 'Responsabile F&B',
  [RuoloUtente.BookingAgent]: 'Agente prenotazioni',
  [RuoloUtente.NightAuditor]: 'Night auditor',
  [RuoloUtente.Marketing]: 'Marketing',
  [RuoloUtente.Owner]: 'Proprietario',
}

function contaPermessi(a: AssegnazioneStrutturaDto): number {
  return [
    a.bookingRead, a.bookingWrite, a.reservationRead, a.reservationWrite, a.statePoliceRead, a.statePoliceWrite,
    a.statePoliceSettings, a.settingAgency, a.settingUser, a.settingRoomRead, a.settingRoomWrite, a.roomStatusUpdate,
    a.financeRead, a.financeWrite, a.restaurantRead, a.restaurantWrite,
  ].filter(Boolean).length
}

export function UtentiPage() {
  const mobile = useMobile()
  const { strutturaId, strutturaCorrente } = useStruttura()
  const { sessione } = useAuth()
  const clienteId = strutturaCorrente?.clienteId ?? null

  const assegnazioni = useAssegnazioniStruttura(strutturaId)
  const utentiCliente = useUtentiCliente(clienteId)
  const rimuoviAssegnazione = useRimuoviAssegnazione(strutturaId)
  const toast = useToast()

  const [dialogo, setDialogo] = useState<StatoAssegnazioneIniziale | null>(null)
  const [daEliminare, setDaEliminare] = useState<AssegnazioneStrutturaDto | null>(null)

  // Il titolare (accesso libero a tutte le Strutture del Cliente, mai bisogno di un'assegnazione)
  // non va offerto tra gli utenti "da assegnare" a questa struttura.
  const utentiNonAssegnati = (utentiCliente.data ?? []).filter(
    (u) => !u.isClienteAccount && !(assegnazioni.data ?? []).some((a) => a.utenteId === u.id),
  )

  // "Assegna utente esistente" mescola utenti di Strutture diverse dello stesso Cliente — solo il
  // titolare e il Super Admin devono poterlo fare, non un lavoratore con solo il permesso SettingUser
  // sulla struttura corrente.
  const puoAssegnareUtenteEsistente = sessione?.isSuperAdmin || sessione?.isClienteAccount

  function eliminaConfermato() {
    if (!daEliminare) return
    rimuoviAssegnazione.mutate(daEliminare.utenteId, {
      onSuccess: () => {
        toast.successo(`Accesso rimosso per ${daEliminare.email}.`)
        setDaEliminare(null)
      },
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <CambiaPasswordCard />

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Utenti con accesso a questa struttura</Typography>
        <Box sx={{ display: 'flex', gap: 1.5 }}>
          {puoAssegnareUtenteEsistente && (
            <BottoneNuovo
              etichetta="+ Assegna utente esistente"
              variant="outlined"
              onClick={() => setDialogo({ modo: 'assegna' })}
              disabilitato={!strutturaId}
            />
          )}
          <BottoneNuovo etichetta="+ Nuovo utente" onClick={() => setDialogo({ modo: 'nuovo' })} disabilitato={!strutturaId} />
        </Box>
      </Box>

      {(assegnazioni.isLoading || utentiCliente.isLoading) && <Skeleton variant="rounded" height={220} />}

      {!assegnazioni.isLoading && !utentiCliente.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(assegnazioni.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessun utente ha ancora accesso a questa struttura." />}
          {(assegnazioni.data ?? []).map((a) => (
            <CardElenco key={a.id}>
              <TestataCardElenco
                titolo={a.email}
                sottotitolo={a.nome || a.cognome ? `${a.nome ?? ''} ${a.cognome ?? ''}`.trim() : undefined}
                azioneDestra={<Chip size="small" label={ETICHETTA_RUOLO[a.ruolo]} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />}
              />
              <RigaCardMeta voci={[{ etichetta: 'Permessi attivi', valore: `${contaPermessi(a)}/16` }]} />
              <AzioniCardElenco>
                <IconButton size="small" onClick={() => setDialogo({ modo: 'modifica', assegnazione: a })}>
                  <EditIcon fontSize="small" />
                </IconButton>
                {a.utenteId !== sessione?.utenteId && (
                  <IconButton size="small" color="error" onClick={() => setDaEliminare(a)}>
                    <DeleteOutlineIcon fontSize="small" />
                  </IconButton>
                )}
              </AzioniCardElenco>
            </CardElenco>
          ))}
        </Box>
      )}

      {!assegnazioni.isLoading && !utentiCliente.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Utente</TableCell>
                <TableCell>Ruolo</TableCell>
                <TableCell align="right">Permessi attivi</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(assegnazioni.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun utente ha ancora accesso a questa struttura.
                  </TableCell>
                </TableRow>
              )}
              {(assegnazioni.data ?? []).map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>
                    {a.email}
                    {(a.nome || a.cognome) && (
                      <Typography component="span" sx={{ fontSize: 12, color: tokens.textSecondary, ml: 1 }}>
                        {a.nome} {a.cognome}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Chip size="small" label={ETICHETTA_RUOLO[a.ruolo]} sx={{ bgcolor: tokens.blue600, color: '#fff', fontWeight: 700 }} />
                  </TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono }}>
                    {contaPermessi(a)}/16
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo({ modo: 'modifica', assegnazione: a })}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    {a.utenteId !== sessione?.utenteId && (
                      <IconButton size="small" color="error" onClick={() => setDaEliminare(a)}>
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo && strutturaId && (
        <AssegnaRuoloDialog strutturaId={strutturaId} clienteId={clienteId} stato={dialogo} utentiDisponibili={utentiNonAssegnati} onClose={() => setDialogo(null)} />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Elimina accesso"
          messaggio={`${daEliminare.email} non avrà più accesso a questa struttura. Se ha accesso anche ad altre strutture, quelle non vengono toccate.`}
          testoConferma="Elimina"
          inCorso={rimuoviAssegnazione.isPending}
          onConferma={eliminaConfermato}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}

function CambiaPasswordCard() {
  const mobile = useMobile()
  const [passwordAttuale, setPasswordAttuale] = useState('')
  const [passwordNuova, setPasswordNuova] = useState('')
  const toast = useToast()

  const cambiaPassword = useCambiaPasswordPropria()

  function salva() {
    if (passwordAttuale.trim() === '' || passwordNuova.trim() === '') {
      toast.errore('Compila entrambi i campi.')
      return
    }
    cambiaPassword.mutate(
      { passwordAttuale, passwordNuova },
      {
        onSuccess: () => {
          toast.successo('Password aggiornata.')
          setPasswordAttuale('')
          setPasswordNuova('')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 560 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Cambia la tua password</Typography>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Password attuale" type="password" value={passwordAttuale} onChange={(e) => setPasswordAttuale(e.target.value)} fullWidth disabled={cambiaPassword.isPending} />
        <TextField label="Nuova password" type="password" value={passwordNuova} onChange={(e) => setPasswordNuova(e.target.value)} fullWidth disabled={cambiaPassword.isPending} />
      </Box>
      <Box>
        <Button variant="contained" color="primary" onClick={salva} disabled={cambiaPassword.isPending}>
          Aggiorna password
        </Button>
      </Box>
    </Box>
  )
}
