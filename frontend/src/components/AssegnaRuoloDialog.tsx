import { useState } from 'react'
import Alert from '@mui/material/Alert'
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
import {
  RuoloUtente,
  useAssegnaRuolo,
  useCreaUtente,
  type AssegnaRuoloRequest,
  type AssegnazioneStrutturaDto,
  type PermessiStruttura,
  type UtenteDto,
} from '../api/utenti'
import { tokens } from '../theme'

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
  const modifica = stato.modo === 'modifica' ? stato.assegnazione : null

  const [utenteEsistenteId, setUtenteEsistenteId] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [nome, setNome] = useState('')
  const [cognome, setCognome] = useState('')
  const [ruolo, setRuolo] = useState<string>(modifica ? String(modifica.ruolo) : String(RuoloUtente.Receptionist))
  const [permessi, setPermessi] = useState<PermessiStruttura>(
    modifica ?? { ...PERMESSI_VUOTI, ...PRESET_PERMESSI[Number(ruolo) as RuoloUtente] },
  )
  const [errore, setErrore] = useState<string | null>(null)

  const creaUtente = useCreaUtente()
  const assegnaRuolo = useAssegnaRuolo(strutturaId)
  const inCorso = creaUtente.isPending || assegnaRuolo.isPending

  function cambiaRuolo(nuovoRuolo: string) {
    setRuolo(nuovoRuolo)
    if (!modifica) {
      setPermessi({ ...PERMESSI_VUOTI, ...PRESET_PERMESSI[Number(nuovoRuolo) as RuoloUtente] })
    }
  }

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function salva() {
    setErrore(null)

    if (stato.modo === 'assegna' && utenteEsistenteId === '') {
      setErrore('Seleziona un utente.')
      return
    }
    if (stato.modo === 'nuovo' && (email.trim() === '' || password.trim() === '')) {
      setErrore('Email e password sono obbligatorie.')
      return
    }

    const request: AssegnaRuoloRequest = { ruolo: Number(ruolo) as RuoloUtente, ...permessi }

    if (modifica) {
      assegnaRuolo.mutate({ utenteId: modifica.utenteId, request }, { onSuccess: onClose, onError: gestisciErrore })
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

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        {modifica ? `Modifica ruolo — ${modifica.email}` : stato.modo === 'nuovo' ? 'Nuovo utente' : 'Assegna utente esistente'}
      </DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        {stato.modo === 'assegna' && (
          <TextField select label="Utente" value={utenteEsistenteId} onChange={(e) => setUtenteEsistenteId(e.target.value)} required disabled={inCorso}>
            {utentiDisponibili.length === 0 && <MenuItem value="">Nessun utente disponibile (già tutti assegnati)</MenuItem>}
            {utentiDisponibili.map((u) => (
              <MenuItem key={u.id} value={u.id}>
                {u.email} {u.nome ? `— ${u.nome} ${u.cognome ?? ''}` : ''}
              </MenuItem>
            ))}
          </TextField>
        )}

        {stato.modo === 'nuovo' && (
          <>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required fullWidth disabled={inCorso} />
              <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required fullWidth disabled={inCorso} />
            </Box>
            <Box sx={{ display: 'flex', gap: 2 }}>
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
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {modifica ? 'Salva modifiche' : 'Salva'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
