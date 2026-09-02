import { useEffect, useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
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
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import type { CameraDto } from '../api/camere'
import type { CanaleVenditaDto } from '../api/canaliVendita'
import type { TipologiaCameraDto } from '../api/tipologie'
import {
  useAggiornaPrenotazione,
  useAnnullaPrenotazione,
  useCheckIn,
  useCheckOut,
  useCreaPrenotazione,
  usePreventivo,
  useVerificaDisponibilita,
  StatoPrenotazione,
  type DisponibilitaCameraDto,
  type PrenotazioneDto,
  type PrenotazioneRequest,
} from '../api/prenotazioni'
import { ApiError } from '../api/client'
import { aggiungiGiorni, formatoInputData, inizioGiornoLocale, isoLocale, parsaInputData } from '../lib/date'
import { CampoData } from './CampoData'
import { tokens } from '../theme'
import { OspiteDialog } from './OspiteDialog'
import { ConfirmDialog } from './ConfirmDialog'

export type StatoIniziale =
  | { modo: 'crea'; cameraId: string | null; checkIn: Date; checkOut: Date }
  | { modo: 'modifica'; prenotazione: PrenotazioneDto }

export const ETICHETTA_STATO: Record<StatoPrenotazione, string> = {
  [StatoPrenotazione.Incompleta]: 'Incompleta',
  [StatoPrenotazione.InCorso]: 'In corso',
  [StatoPrenotazione.Completata]: 'Completata',
  [StatoPrenotazione.Annullata]: 'Annullata',
}

export const COLORE_STATO: Record<StatoPrenotazione, string> = {
  [StatoPrenotazione.Incompleta]: tokens.wait600,
  [StatoPrenotazione.InCorso]: tokens.blue600,
  [StatoPrenotazione.Completata]: tokens.ok600,
  [StatoPrenotazione.Annullata]: tokens.error600,
}

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

function messaggioConflitto(conflitto: DisponibilitaCameraDto): string {
  const dal = conflitto.checkIn ? formattatoreData.format(new Date(conflitto.checkIn)) : '?'
  const al = conflitto.checkOut ? formattatoreData.format(new Date(conflitto.checkOut)) : '?'
  return `Questa camera è già prenotata dal ${dal} al ${al}.`
}

interface Props {
  strutturaId: string
  stato: StatoIniziale
  camere: CameraDto[]
  canali: CanaleVenditaDto[]
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function PrenotazioneDialog({ strutturaId, stato, camere, canali, tipologie, onClose }: Props) {
  const modifica = stato.modo === 'modifica' ? stato.prenotazione : null
  const creaIniziale = stato.modo === 'crea' ? stato : null

  const cameraInizialeId = modifica ? modifica.cameraId ?? '' : creaIniziale!.cameraId ?? ''
  const tipologiaInizialeId = camere.find((c) => c.id === cameraInizialeId)?.tipologiaId ?? ''

  // Nel form la Tipologia va scelta per prima: appena selezionata, la Camera si filtra a quelle di
  // quella tipologia (entrambe cercabili). Non è un campo inviato al backend, solo la guida alla
  // scelta della Camera.
  const [tipologiaFiltroId, setTipologiaFiltroId] = useState(tipologiaInizialeId)
  const [cameraId, setCameraId] = useState<string>(cameraInizialeId)
  const [agenzia, setAgenzia] = useState(modifica?.agenzia ?? '')
  const [numeroPrenotazione, setNumeroPrenotazione] = useState(modifica?.numeroPrenotazione ?? '')
  const [checkIn, setCheckIn] = useState(formatoInputData(modifica ? inizioGiornoLocale(new Date(modifica.checkIn!)) : creaIniziale!.checkIn))
  const [checkOut, setCheckOut] = useState(formatoInputData(modifica ? inizioGiornoLocale(new Date(modifica.checkOut!)) : creaIniziale!.checkOut))
  const [numeroOspiti, setNumeroOspiti] = useState<string>(String(modifica?.numeroOspiti ?? 2))
  const [importoTotale, setImportoTotale] = useState<string>(modifica?.importoTotale != null ? String(modifica.importoTotale) : '')
  // Finché l'operatore non tocca il campo a mano, "Importo totale" segue il preventivo — se cambi
  // camera/date/checkbox si aggiorna da solo, senza dover recliccare "Usa" ogni volta.
  const [importoTotaleAuto, setImportoTotaleAuto] = useState(!modifica)
  const [importoPagato, setImportoPagato] = useState<string>(modifica?.importoPagato != null ? String(modifica.importoPagato) : '')
  // La sezione (e questo checkbox) compare solo quando la cauzione si applica davvero a questa
  // prenotazione (tipologia con importo configurato E toggle "Cauzione" attivo) — quando è
  // mostrata, il default sensato è restituirla per intero.
  const [restituisciCauzione, setRestituisciCauzione] = useState(true)
  const [importoCauzioneTrattenuta, setImportoCauzioneTrattenuta] = useState('')
  const [tassaSoggiornoAttiva, setTassaSoggiornoAttiva] = useState(modifica?.tassaSoggiornoAttiva ?? true)
  const [spesePuliziaAttiva, setSpesePuliziaAttiva] = useState(modifica?.spesePuliziaAttiva ?? true)
  // A differenza degli altri toggle, default false: si applica solo se l'ospite porta un animale.
  const [animaliAttiva, setAnimaliAttiva] = useState(modifica?.animaliAttiva ?? false)
  const [cauzioneAttiva, setCauzioneAttiva] = useState(modifica?.cauzioneAttiva ?? true)
  const [errore, setErrore] = useState<string | null>(null)
  const [schedaOspitiAperta, setSchedaOspitiAperta] = useState(false)
  const [confermaAnnullaAperta, setConfermaAnnullaAperta] = useState(false)
  // Appena creata una nuova prenotazione, si passa direttamente alla scheda ospiti (Nome/Cognome
  // veri, non un testo libero da spezzare a indovinare) — compilabile subito o saltabile del tutto.
  const [prenotazioneAppenaCreata, setPrenotazioneAppenaCreata] = useState<PrenotazioneDto | null>(null)

  const crea = useCreaPrenotazione(strutturaId)
  const aggiorna = useAggiornaPrenotazione(strutturaId)
  const annulla = useAnnullaPrenotazione(strutturaId)
  const checkInMutation = useCheckIn(strutturaId)
  const checkOutMutation = useCheckOut(strutturaId)

  const cameraSelezionata = camere.find((c) => c.id === cameraId)
  const tipologiaSelezionata = tipologie.find((t) => t.id === cameraSelezionata?.tipologiaId)
  // Cauzione/Animali sono solo informazioni interne (si gestiscono di persona, non generano invii
  // esterni): se la tipologia della camera non ne prevede un importo, non ha senso mostrare il toggle.
  const cauzionePrevista = (tipologiaSelezionata?.cauzione ?? 0) > 0
  const animaliPrevisti = (tipologiaSelezionata?.animali ?? 0) > 0

  // Camere della tipologia scelta nel form — include comunque la camera già assegnata anche se non
  // corrisponde più alla tipologia selezionata, per non nascondere un'associazione esistente.
  const camereTipologia = camere.filter((c) => c.tipologiaId === tipologiaFiltroId || c.id === cameraId)

  const checkInDate = checkIn ? parsaInputData(checkIn) : null
  const checkOutDate = checkOut ? parsaInputData(checkOut) : null
  const dateValide = !!checkInDate && !!checkOutDate && checkOutDate > checkInDate
  const numeroOspitiNumero = Number(numeroOspiti) || 0

  // Il preventivo ha senso solo per una prenotazione nuova: su una già esistente il prezzo pattuito
  // è quello salvato (Importo totale), non va ricalcolato/riproposto ogni volta che si riapre.
  const preventivo = usePreventivo(
    strutturaId,
    cameraId || null,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    numeroOspitiNumero,
    spesePuliziaAttiva,
    animaliPrevisti && animaliAttiva,
    cauzionePrevista && cauzioneAttiva,
    !modifica,
  )

  useEffect(() => {
    if (importoTotaleAuto && preventivo.data) {
      setImportoTotale(String(preventivo.data.totale))
    }
  }, [preventivo.data, importoTotaleAuto])

  // Controllo live di sovrapposizione: appena camera+date sono selezionate, prima ancora di
  // premere "Crea"/"Salva", segnala se quella camera è già occupata in quel periodo — il controllo
  // autorevole (che blocca davvero il salvataggio con lo stesso messaggio) resta comunque lato server.
  const disponibilita = useVerificaDisponibilita(
    strutturaId,
    cameraId || null,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    modifica?.id ?? null,
    !!cameraId && dateValide,
  )
  const conflitto = disponibilita.data && !disponibilita.data.disponibile ? disponibilita.data : null

  // Per "Diretta" il numero è sempre auto-generato dal backend al salvataggio: il campo si nasconde
  // e si azzera per non lasciare in giro un valore digitato prima di passare a Diretta, che
  // altrimenti verrebbe inviato come se fosse stato scelto a mano.
  useEffect(() => {
    if (agenzia === 'Diretta' && numeroPrenotazione !== '') {
      setNumeroPrenotazione('')
    }
  }, [agenzia, numeroPrenotazione])

  const inCorso = crea.isPending || aggiorna.isPending || annulla.isPending || checkInMutation.isPending || checkOutMutation.isPending
  // Un soggiorno Completato è chiuso: resta modificabile solo il saldo (Importo totale/Importo
  // pagato), tutto il resto (camera, date, toggle...) è quello con cui si è effettivamente svolto.
  const soloImporti = modifica?.statoPrenotazione === StatoPrenotazione.Completata

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
      tassaSoggiornoAttiva,
      spesePuliziaAttiva,
      animaliAttiva: animaliPrevisti && animaliAttiva,
      cauzioneAttiva: cauzionePrevista && cauzioneAttiva,
    }

    if (modifica) {
      aggiorna.mutate({ prenotazioneId: modifica.id, request }, { onSuccess: onClose, onError: gestisciErrore })
    } else {
      crea.mutate(request, { onSuccess: setPrenotazioneAppenaCreata, onError: gestisciErrore })
    }
  }

  function eseguiAnnulla() {
    if (!modifica) return
    setConfermaAnnullaAperta(true)
  }

  function confermaAnnulla() {
    if (!modifica) return
    annulla.mutate(modifica.id, {
      onSuccess: onClose,
      onError: (err) => {
        setConfermaAnnullaAperta(false)
        gestisciErrore(err)
      },
    })
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

  if (prenotazioneAppenaCreata) {
    return <OspiteDialog strutturaId={strutturaId} prenotazione={prenotazioneAppenaCreata} onClose={onClose} />
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {modifica ? `Prenotazione ${modifica.numeroPrenotazione ? `#${modifica.numeroPrenotazione}` : ''}` : 'Nuova prenotazione'}
        {stato_ && <Chip size="small" label={ETICHETTA_STATO[stato_]} sx={{ bgcolor: COLORE_STATO[stato_], color: '#fff', fontWeight: 700 }} />}
      </DialogTitle>

      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {/* Il Box (invece di renderizzare {errore && ...} nudo) garantisce che il campo Tipologia
            sotto non sia mai il primo figlio letterale del contenitore flex quando non c'è errore:
            un Autocomplete in quella posizione esatta mostra la label ristretta tagliata a metà dal
            bordo (bug reale di rendering riprodotto e isolato, non specifico di un singolo campo). */}
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <Autocomplete
            sx={{ flex: 1 }}
            options={tipologie}
            getOptionLabel={(t) => t.tipologiaCamera}
            value={tipologie.find((t) => t.id === tipologiaFiltroId) ?? null}
            onChange={(_, valore) => {
              setTipologiaFiltroId(valore?.id ?? '')
              setCameraId('')
            }}
            disabled={inCorso || soloImporti}
            noOptionsText="Nessuna tipologia disponibile"
            renderInput={(params) => <TextField {...params} label="Tipologia" required placeholder="Cerca per nome…" />}
          />
          <Autocomplete
            sx={{ flex: 1 }}
            options={camereTipologia}
            getOptionLabel={(c) => c.nome}
            value={camereTipologia.find((c) => c.id === cameraId) ?? null}
            onChange={(_, valore) => setCameraId(valore?.id ?? '')}
            disabled={inCorso || soloImporti || tipologiaFiltroId === ''}
            noOptionsText="Nessuna camera per questa tipologia"
            renderInput={(params) => <TextField {...params} label="Camera" required placeholder="Cerca per nome…" />}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <CampoData label="Check-in" value={checkIn} onChange={setCheckIn} fullWidth disabled={inCorso || soloImporti} />
          <CampoData
            label="Check-out"
            value={checkOut}
            onChange={setCheckOut}
            // Il check-out non può mai essere uguale o precedente al check-in: il calendario si apre
            // già sul mese del check-in (se in un mese futuro) e non permette di scegliere prima.
            min={checkIn ? formatoInputData(aggiungiGiorni(parsaInputData(checkIn), 1)) : undefined}
            fullWidth
            error={!dateValide}
            helperText={!dateValide ? 'Deve essere dopo il check-in' : ' '}
            disabled={inCorso || soloImporti}
          />
        </Box>

        {conflitto && <Alert severity="warning">{messaggioConflitto(conflitto)}</Alert>}

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            select
            label="Agenzia / canale"
            value={agenzia}
            onChange={(e) => setAgenzia(e.target.value)}
            fullWidth
            disabled={inCorso || soloImporti}
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
            disabled={inCorso || soloImporti}
            slotProps={{ htmlInput: { min: 1 } }}
          />
        </Box>

        {agenzia !== 'Diretta' && (
          <TextField
            label="Numero prenotazione"
            value={numeroPrenotazione}
            onChange={(e) => setNumeroPrenotazione(e.target.value)}
            disabled={inCorso || soloImporti}
          />
        )}

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Importo totale (€)"
            type="number"
            value={importoTotale}
            onChange={(e) => {
              setImportoTotale(e.target.value)
              setImportoTotaleAuto(false)
            }}
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

        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
          <FormControlLabel
            control={<Checkbox checked={spesePuliziaAttiva} onChange={(e) => setSpesePuliziaAttiva(e.target.checked)} disabled={inCorso || soloImporti} />}
            label="Spese di pulizia"
          />
          {animaliPrevisti && (
            <FormControlLabel
              control={<Checkbox checked={animaliAttiva} onChange={(e) => setAnimaliAttiva(e.target.checked)} disabled={inCorso || soloImporti} />}
              label="Animali"
            />
          )}
          {cauzionePrevista && (
            <FormControlLabel
              control={<Checkbox checked={cauzioneAttiva} onChange={(e) => setCauzioneAttiva(e.target.checked)} disabled={inCorso || soloImporti} />}
              label="Cauzione"
            />
          )}
          <Tooltip title="Se disattivata, le schedine Alloggiati Web/Osservatorio/PayTourist non vengono inviate per questa prenotazione — vengono segnate come già inviate. Riattivandola tornano tra quelle da inviare.">
            <FormControlLabel
              control={<Checkbox checked={tassaSoggiornoAttiva} onChange={(e) => setTassaSoggiornoAttiva(e.target.checked)} disabled={inCorso || soloImporti} />}
              label="Tassa di soggiorno"
            />
          </Tooltip>
        </Box>

        {modifica && modifica.statoPrenotazione === StatoPrenotazione.InCorso && cauzionePrevista && cauzioneAttiva && (
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
        {modifica && (
          <Button onClick={() => setSchedaOspitiAperta(true)} disabled={inCorso}>
            Scheda ospiti
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

      {schedaOspitiAperta && modifica && (
        <OspiteDialog
          strutturaId={strutturaId}
          prenotazione={modifica}
          onClose={() => setSchedaOspitiAperta(false)}
          onApriPrenotazione={() => setSchedaOspitiAperta(false)}
        />
      )}

      {confermaAnnullaAperta && (
        <ConfirmDialog
          titolo="Annullare prenotazione"
          messaggio="Annullare questa prenotazione? Gli importi verranno azzerati."
          inCorso={annulla.isPending}
          onConferma={confermaAnnulla}
          onAnnulla={() => setConfermaAnnullaAperta(false)}
        />
      )}
    </Dialog>
  )
}
