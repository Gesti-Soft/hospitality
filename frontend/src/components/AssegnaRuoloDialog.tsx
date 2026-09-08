import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { useMobile } from '../lib/useMobile'
import {
  RuoloUtente,
  useAggiornaUtente,
  useAssegnaRuolo,
  useCreaUtente,
  useResetPasswordUtente,
  type AssegnaRuoloRequest,
  type AssegnazioneStrutturaDto,
  type PermessiStruttura,
  type UtenteDto,
} from '../api/utenti'
import { tokens } from '../theme'
import { useToast } from '../toast/ToastContext'

export type StatoAssegnazioneIniziale = { modo: 'nuovo' } | { modo: 'assegna' } | { modo: 'modifica'; assegnazione: AssegnazioneStrutturaDto }

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

const PERMESSI_VUOTI: PermessiStruttura = {
  bookingRead: false,
  bookingWrite: false,
  reservationRead: false,
  reservationWrite: false,
  statePoliceRead: false,
  statePoliceWrite: false,
  statePoliceSettings: false,
  settingAgency: false,
  settingUser: false,
  settingRoomRead: false,
  settingRoomWrite: false,
  roomStatusUpdate: false,
  financeRead: false,
  financeWrite: false,
  restaurantRead: false,
  restaurantWrite: false,
}

const TUTTI_PERMESSI: PermessiStruttura = Object.fromEntries(Object.keys(PERMESSI_VUOTI).map((k) => [k, true])) as unknown as PermessiStruttura

// Preset di comodo: solo un punto di partenza, ogni permesso resta modificabile prima di salvare.
const PRESET_PERMESSI: Record<RuoloUtente, Partial<PermessiStruttura>> = {
  [RuoloUtente.Administrator]: TUTTI_PERMESSI,
  [RuoloUtente.Owner]: TUTTI_PERMESSI,
  [RuoloUtente.Manager]: TUTTI_PERMESSI,
  [RuoloUtente.Receptionist]: {
    reservationRead: true,
    reservationWrite: true,
    roomStatusUpdate: true,
    settingRoomRead: true,
    statePoliceRead: true,
    statePoliceWrite: true,
  },
  [RuoloUtente.Housekeeper]: { roomStatusUpdate: true, settingRoomRead: true },
  [RuoloUtente.Accountant]: { financeRead: true, financeWrite: true, reservationRead: true },
  [RuoloUtente.Maintenance]: { roomStatusUpdate: true, settingRoomRead: true },
  [RuoloUtente.FnbManager]: { restaurantRead: true, restaurantWrite: true },
  [RuoloUtente.BookingAgent]: { bookingRead: true, bookingWrite: true, reservationRead: true, reservationWrite: true },
  [RuoloUtente.NightAuditor]: { reservationRead: true, reservationWrite: true, financeRead: true, roomStatusUpdate: true },
  [RuoloUtente.Marketing]: { bookingRead: true, reservationRead: true },
}

const GRUPPI_PERMESSI: { titolo: string; voci: { chiave: keyof PermessiStruttura; etichetta: string }[] }[] = [
  {
    titolo: 'Prenotazioni',
    voci: [
      { chiave: 'reservationRead', etichetta: 'Consulta' },
      { chiave: 'reservationWrite', etichetta: 'Crea/modifica' },
      { chiave: 'roomStatusUpdate', etichetta: 'Check-in/out, stato camera' },
    ],
  },
  {
    titolo: 'Camere e tariffe',
    voci: [
      { chiave: 'settingRoomRead', etichetta: 'Consulta' },
      { chiave: 'settingRoomWrite', etichetta: 'Crea/modifica' },
      { chiave: 'settingAgency', etichetta: 'Canali vendita' },
    ],
  },
  {
    titolo: 'Finanze e fatturazione',
    voci: [
      { chiave: 'financeRead', etichetta: 'Consulta' },
      { chiave: 'financeWrite', etichetta: 'Crea/modifica' },
    ],
  },
  {
    titolo: 'Polizia di Stato / invii automatici',
    voci: [
      { chiave: 'statePoliceRead', etichetta: 'Consulta' },
      { chiave: 'statePoliceWrite', etichetta: 'Invia' },
      { chiave: 'statePoliceSettings', etichetta: 'Configura credenziali' },
    ],
  },
  {
    titolo: 'Ristorante',
    voci: [
      { chiave: 'restaurantRead', etichetta: 'Consulta' },
      { chiave: 'restaurantWrite', etichetta: 'Crea/modifica' },
    ],
  },
  {
    titolo: 'Booking board (riservato)',
    voci: [
      { chiave: 'bookingRead', etichetta: 'Consulta' },
      { chiave: 'bookingWrite', etichetta: 'Crea/modifica' },
    ],
  },
  {
    titolo: 'Amministrazione',
    voci: [{ chiave: 'settingUser', etichetta: 'Gestione utenti' }],
  },
]

interface Props {
  strutturaId: string
  clienteId: string | null
  stato: StatoAssegnazioneIniziale
  utentiDisponibili: UtenteDto[]
  onClose: () => void
}

export function AssegnaRuoloDialog({ strutturaId, clienteId, stato, utentiDisponibili, onClose }: Props) {
  const mobile = useMobile()
  const modifica = stato.modo === 'modifica' ? stato.assegnazione : null

  const [utenteEsistenteId, setUtenteEsistenteId] = useState('')
  const [email, setEmail] = useState(modifica?.email ?? '')
  const [password, setPassword] = useState('')
  const [nome, setNome] = useState(modifica?.nome ?? '')
  const [cognome, setCognome] = useState(modifica?.cognome ?? '')
  const [ruolo, setRuolo] = useState<string>(modifica ? String(modifica.ruolo) : String(RuoloUtente.Receptionist))
  const [permessi, setPermessi] = useState<PermessiStruttura>(
    modifica ?? { ...PERMESSI_VUOTI, ...PRESET_PERMESSI[Number(ruolo) as RuoloUtente] },
  )
  const [errore, setErrore] = useState<string | null>(null)
  const [nuovaPassword, setNuovaPassword] = useState('')
  const toast = useToast()

  const creaUtente = useCreaUtente()
  const assegnaRuolo = useAssegnaRuolo(strutturaId)
  const aggiornaUtente = useAggiornaUtente()
  const resetPassword = useResetPasswordUtente()
  const inCorso = creaUtente.isPending || assegnaRuolo.isPending || aggiornaUtente.isPending

  // L'autorizzazione vera è determinata solo dai permessi granulari, mai dal campo Ruolo (che è
  // solo un'etichetta descrittiva, vedi RuoloUtente) — cambiare qui il ruolo deve quindi
  // riapplicare subito il preset di permessi di quel ruolo, altrimenti selezionare "Amministratore"
  // su un utente già assegnato come Receptionist cambierebbe solo l'etichetta mostrata, lasciando
  // i permessi reali (e quindi il comportamento dell'utente nell'app) invariati — bug reale
  // segnalato dall'utente ("ho messo admin ma mi lascia sempre receptionist").
  function cambiaRuolo(nuovoRuolo: string) {
    setRuolo(nuovoRuolo)
    setPermessi({ ...PERMESSI_VUOTI, ...PRESET_PERMESSI[Number(nuovoRuolo) as RuoloUtente] })
  }

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  async function salva() {
    setErrore(null)

    if (stato.modo === 'assegna' && utenteEsistenteId === '') {
      setErrore('Seleziona un utente.')
      return
    }
    if (stato.modo === 'nuovo' && (email.trim() === '' || password.trim() === '')) {
      setErrore('Email e password sono obbligatorie.')
      return
    }
    if (modifica && email.trim() === '') {
      setErrore('Email obbligatoria.')
      return
    }

    const request: AssegnaRuoloRequest = { ruolo: Number(ruolo) as RuoloUtente, ...permessi }

    if (modifica) {
      try {
        await aggiornaUtente.mutateAsync({
          utenteId: modifica.utenteId,
          request: { email: email.trim(), nome: nome.trim() === '' ? null : nome.trim(), cognome: cognome.trim() === '' ? null : cognome.trim() },
        })
        await assegnaRuolo.mutateAsync({ utenteId: modifica.utenteId, request })
        onClose()
      } catch (err) {
        gestisciErrore(err)
      }
    } else if (stato.modo === 'assegna') {
      assegnaRuolo.mutate({ utenteId: utenteEsistenteId, request }, { onSuccess: onClose, onError: gestisciErrore })
    } else {
      creaUtente.mutate(
        { email: email.trim(), password, nome: nome.trim() === '' ? null : nome.trim(), cognome: cognome.trim() === '' ? null : cognome.trim(), isSuperAdmin: false, clienteId },
        {
          onSuccess: (nuovoUtente) => assegnaRuolo.mutate({ utenteId: nuovoUtente.id, request }, { onSuccess: onClose, onError: gestisciErrore }),
          onError: gestisciErrore,
        },
      )
    }
  }

  function reimpostaPassword() {
    if (!modifica) return
    if (nuovaPassword.trim().length < 6) {
      toast.errore('La nuova password deve avere almeno 6 caratteri.')
      return
    }
    resetPassword.mutate(
      { utenteId: modifica.utenteId, passwordNuova: nuovaPassword },
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>
        {modifica ? `Modifica utente — ${modifica.email}` : stato.modo === 'nuovo' ? 'Nuovo utente' : 'Assegna utente esistente'}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        {stato.modo === 'assegna' && (
          <Autocomplete
            options={utentiDisponibili}
            getOptionLabel={(u) => `${u.email}${u.nome ? ` — ${u.nome} ${u.cognome ?? ''}` : ''}`}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            value={utentiDisponibili.find((u) => u.id === utenteEsistenteId) ?? null}
            onChange={(_, v) => setUtenteEsistenteId(v?.id ?? '')}
            disabled={inCorso}
            noOptionsText="Nessun utente disponibile (già tutti assegnati)"
            renderInput={(params) => <TextField {...params} label="Utente" required placeholder="Cerca per email o nome…" />}
          />
        )}

        {stato.modo === 'nuovo' && (
          <>
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required fullWidth disabled={inCorso} />
              <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required fullWidth disabled={inCorso} />
            </Box>
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
              <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
            </Box>
          </>
        )}

        {modifica && (
          <>
            <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required fullWidth disabled={inCorso} />
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
              <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
            </Box>
          </>
        )}

        <TextField select label="Ruolo" value={ruolo} onChange={(e) => cambiaRuolo(e.target.value)} disabled={inCorso}>
          {Object.entries(ETICHETTA_RUOLO).map(([valore, etichetta]) => (
            <MenuItem key={valore} value={valore}>
              {etichetta}
            </MenuItem>
          ))}
        </TextField>

        <Divider />
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
          Permessi su questa struttura (il ruolo suggerisce un punto di partenza, ogni voce resta modificabile).
        </Typography>

        {GRUPPI_PERMESSI.map((gruppo) => (
          <Box key={gruppo.titolo}>
            <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: tokens.textSecondary, mb: 0.25 }}>{gruppo.titolo}</Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {gruppo.voci.map((voce) => (
                <FormControlLabel
                  key={voce.chiave}
                  sx={{ mr: 2 }}
                  control={
                    <Checkbox
                      size="small"
                      checked={permessi[voce.chiave]}
                      onChange={(e) => setPermessi((prec) => ({ ...prec, [voce.chiave]: e.target.checked }))}
                      disabled={inCorso}
                    />
                  }
                  label={<Typography sx={{ fontSize: 13 }}>{voce.etichetta}</Typography>}
                />
              ))}
            </Box>
          </Box>
        ))}

        {modifica && (
          <>
            <Divider />
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>Reimposta la password di questo utente (non serve conoscere quella attuale).</Typography>
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2, alignItems: mobile ? 'stretch' : 'flex-start' }}>
              <TextField
                label="Nuova password"
                type="password"
                value={nuovaPassword}
                onChange={(e) => setNuovaPassword(e.target.value)}
                fullWidth
                disabled={resetPassword.isPending}
              />
              <Button variant="outlined" onClick={reimpostaPassword} disabled={resetPassword.isPending || nuovaPassword.trim() === ''} sx={{ whiteSpace: 'nowrap', flex: '0 0 auto' }}>
                Reimposta
              </Button>
            </Box>
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {modifica ? 'Salva modifiche' : 'Salva'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
