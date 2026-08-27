import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import type { CameraDto } from '../api/camere'
import type { CanaleVenditaDto } from '../api/canaliVendita'
import {
  useAggiornaPrenotazione,
  useAnnullaPrenotazione,
  useCheckIn,
  useCheckOut,
  useCreaPrenotazione,
  usePreventivo,
  StatoPrenotazione,
  type PrenotazioneDto,
  type PrenotazioneRequest,
} from '../api/prenotazioni'
import { ApiError } from '../api/client'
import { formatoInputData, inizioGiornoLocale, isoLocale, parsaInputData } from '../lib/date'
import { fontMono, tokens } from '../theme'

export type StatoIniziale =
  | { modo: 'crea'; cameraId: string | null; checkIn: Date; checkOut: Date }
  | { modo: 'modifica'; prenotazione: PrenotazioneDto }

const ETICHETTA_STATO: Record<StatoPrenotazione, string> = {
  [StatoPrenotazione.Incompleta]: 'Incompleta',
  [StatoPrenotazione.InCorso]: 'In corso',
  [StatoPrenotazione.Completata]: 'Completata',
  [StatoPrenotazione.Annullata]: 'Annullata',
}

const COLORE_STATO: Record<StatoPrenotazione, string> = {
  [StatoPrenotazione.Incompleta]: tokens.wait600,
  [StatoPrenotazione.InCorso]: tokens.blue600,
  [StatoPrenotazione.Completata]: tokens.ok600,
  [StatoPrenotazione.Annullata]: tokens.error600,
}

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

interface Props {
  strutturaId: string
  stato: StatoIniziale
  camere: CameraDto[]
  canali: CanaleVenditaDto[]
  onClose: () => void
}

export function PrenotazioneDialog({ strutturaId, stato, camere, canali, onClose }: Props) {
  const modifica = stato.modo === 'modifica' ? stato.prenotazione : null
  const creaIniziale = stato.modo === 'crea' ? stato : null

  const [cameraId, setCameraId] = useState<string>(modifica ? modifica.cameraId ?? '' : creaIniziale!.cameraId ?? '')
  const [agenzia, setAgenzia] = useState(modifica?.agenzia ?? '')
  const [numeroPrenotazione, setNumeroPrenotazione] = useState(modifica?.numeroPrenotazione ?? '')
  const [checkIn, setCheckIn] = useState(formatoInputData(modifica ? inizioGiornoLocale(new Date(modifica.checkIn!)) : creaIniziale!.checkIn))
  const [checkOut, setCheckOut] = useState(formatoInputData(modifica ? inizioGiornoLocale(new Date(modifica.checkOut!)) : creaIniziale!.checkOut))
  const [numeroOspiti, setNumeroOspiti] = useState<string>(String(modifica?.numeroOspiti ?? 2))
  const [importoTotale, setImportoTotale] = useState<string>(modifica?.importoTotale != null ? String(modifica.importoTotale) : '')
  const [importoPagato, setImportoPagato] = useState<string>(modifica?.importoPagato != null ? String(modifica.importoPagato) : '')
  const [restituisciCauzione, setRestituisciCauzione] = useState(true)
  const [importoCauzioneTrattenuta, setImportoCauzioneTrattenuta] = useState('')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaPrenotazione(strutturaId)
  const aggiorna = useAggiornaPrenotazione(strutturaId)
  const annulla = useAnnullaPrenotazione(strutturaId)
  const checkInMutation = useCheckIn(strutturaId)
  const checkOutMutation = useCheckOut(strutturaId)

  const checkInDate = checkIn ? parsaInputData(checkIn) : null
  const checkOutDate = checkOut ? parsaInputData(checkOut) : null
  const dateValide = !!checkInDate && !!checkOutDate && checkOutDate > checkInDate
  const numeroOspitiNumero = Number(numeroOspiti) || 0

  const preventivo = usePreventivo(
    strutturaId,
    cameraId || null,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    numeroOspitiNumero,
  )

  const inCorso = crea.isPending || aggiorna.isPending || annulla.isPending || checkInMutation.isPending || checkOutMutation.isPending

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function salva() {
    if (!cameraId || !dateValide) {
      setErrore('Seleziona una camera e un periodo valido (check-out dopo il check-in).')
      return
    }
    setErrore(null)

    const request: PrenotazioneRequest = {
      cameraId,
      agenzia: agenzia.trim() === '' ? null : agenzia.trim(),
      numeroPrenotazione: numeroPrenotazione.trim() === '' ? null : numeroPrenotazione.trim(),
      importoPrenotazione: modifica?.importoPrenotazione ?? null,
      importoPagato: importoPagato.trim() === '' ? null : Number(importoPagato),
      importoTotale: importoTotale.trim() === '' ? null : Number(importoTotale),
      checkIn: isoLocale(checkInDate!),
      checkOut: isoLocale(checkOutDate!),
      numeroOspiti: numeroOspitiNumero || null,
    }

    if (modifica) {
      aggiorna.mutate({ prenotazioneId: modifica.id, request }, { onSuccess: onClose, onError: gestisciErrore })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError: gestisciErrore })
    }
  }

  function eseguiAnnulla() {
    if (!modifica) return
    if (!window.confirm('Annullare questa prenotazione? Gli importi verranno azzerati.')) return
    annulla.mutate(modifica.id, { onSuccess: onClose, onError: gestisciErrore })
  }

  function eseguiCheckIn() {
    if (!modifica) return
    checkInMutation.mutate(modifica.id, { onSuccess: onClose, onError: gestisciErrore })
  }

  function eseguiCheckOut() {
    if (!modifica) return
    checkOutMutation.mutate(
      {
        prenotazioneId: modifica.id,
        restituisciCauzione,
        importoCauzioneTrattenuta: restituisciCauzione ? null : importoCauzioneTrattenuta.trim() === '' ? null : Number(importoCauzioneTrattenuta),
      },
      { onSuccess: onClose, onError: gestisciErrore },
    )
  }

  const stato_ = modifica?.statoPrenotazione ?? null

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {modifica ? `Prenotazione ${modifica.numeroPrenotazione ? `#${modifica.numeroPrenotazione}` : ''}` : 'Nuova prenotazione'}
        {stato_ && <Chip size="small" label={ETICHETTA_STATO[stato_]} sx={{ bgcolor: COLORE_STATO[stato_], color: '#fff', fontWeight: 700 }} />}
      </DialogTitle>

      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <TextField select label="Camera" value={cameraId} onChange={(e) => setCameraId(e.target.value)} required disabled={inCorso}>
          {camere.length === 0 && <MenuItem value="">Nessuna camera disponibile</MenuItem>}
          {camere.map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.nome} {c.tipologiaNome ? `— ${c.tipologiaNome}` : ''}
            </MenuItem>
          ))}
        </TextField>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Check-in"
            type="date"
            value={checkIn}
            onChange={(e) => setCheckIn(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={inCorso}
          />
          <TextField
            label="Check-out"
            type="date"
            value={checkOut}
            onChange={(e) => setCheckOut(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            error={!dateValide}
            helperText={!dateValide ? 'Deve essere dopo il check-in' : ' '}
            disabled={inCorso}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            select
            label="Agenzia / canale"
            value={agenzia}
            onChange={(e) => setAgenzia(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ select: { native: false } }}
          >
            <MenuItem value="Diretta">Diretta</MenuItem>
            {canali.filter((c) => c.descrizione !== 'Diretta').map((c) => (
              <MenuItem key={c.id} value={c.descrizione}>
                {c.descrizione}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Numero ospiti"
            type="number"
            value={numeroOspiti}
            onChange={(e) => setNumeroOspiti(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 1 } }}
          />
        </Box>

        <TextField
          label="Numero prenotazione"
          value={numeroPrenotazione}
          onChange={(e) => setNumeroPrenotazione(e.target.value)}
          placeholder="Auto-generato se Diretta e lasciato vuoto"
          disabled={inCorso}
        />

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Importo totale (€)"
            type="number"
            value={importoTotale}
            onChange={(e) => setImportoTotale(e.target.value)}
            fullWidth
            disabled={inCorso}
          />
          <TextField
            label="Importo pagato (€)"
            type="number"
            value={importoPagato}
            onChange={(e) => setImportoPagato(e.target.value)}
            fullWidth
            disabled={inCorso}
          />
        </Box>

        {preventivo.data && (
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', bgcolor: tokens.paper, borderRadius: 1.5, px: 1.5, py: 1 }}>
            <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
              Preventivo: {preventivo.data.notti} nott{preventivo.data.notti === 1 ? 'e' : 'i'}
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography sx={{ fontFamily: fontMono, fontWeight: 700, fontSize: 14 }}>{formattatoreValuta.format(preventivo.data.totale)}</Typography>
              <Button size="small" onClick={() => setImportoTotale(String(preventivo.data!.totale))} disabled={inCorso}>
                Usa
              </Button>
            </Box>
          </Box>
        )}

        {modifica && modifica.statoPrenotazione === StatoPrenotazione.InCorso && (
          <>
            <Divider />
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>Check-out</Typography>
            <FormControlLabel
              control={<Checkbox checked={restituisciCauzione} onChange={(e) => setRestituisciCauzione(e.target.checked)} disabled={inCorso} />}
              label="Restituisci l'intera cauzione al cliente"
            />
            {!restituisciCauzione && (
              <TextField
                label="Importo cauzione trattenuta (€)"
                type="number"
                value={importoCauzioneTrattenuta}
                onChange={(e) => setImportoCauzioneTrattenuta(e.target.value)}
                disabled={inCorso}
              />
            )}
          </>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, pb: 2.5, flexWrap: 'wrap', gap: 1 }}>
        {modifica && modifica.statoPrenotazione !== StatoPrenotazione.Annullata && modifica.statoPrenotazione !== StatoPrenotazione.Completata && (
          <Button color="error" onClick={eseguiAnnulla} disabled={inCorso} sx={{ mr: 'auto' }}>
            Annulla prenotazione
          </Button>
        )}
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        {modifica && modifica.statoPrenotazione === StatoPrenotazione.Incompleta && (
          <Button variant="contained" onClick={eseguiCheckIn} disabled={inCorso}>
            Check-in
          </Button>
        )}
        {modifica && modifica.statoPrenotazione === StatoPrenotazione.InCorso && (
          <Button variant="contained" onClick={eseguiCheckOut} disabled={inCorso}>
            Check-out
          </Button>
        )}
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {modifica ? 'Salva modifiche' : 'Crea prenotazione'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
