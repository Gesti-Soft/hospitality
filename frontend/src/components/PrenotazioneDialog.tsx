import { useEffect, useRef, useState } from 'react'
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
import { aggiungiGiorni, formatoInputData, inizioGiornoLocale, isOggiOPrima, isoLocale, parsaInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { tokens } from '../theme'
import { OspiteDialog } from './OspiteDialog'
import { ConfirmDialog } from './ConfirmDialog'
import { usePuoScrivere } from '../permessi/usePuoScrivere'

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
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('reservationWrite')
  const puoCambiareStatoCamera = usePuoScrivere('roomStatusUpdate')
  const modifica = stato.modo === 'modifica' ? stato.prenotazione : null
  const creaIniziale = stato.modo === 'crea' ? stato : null

  const cameraInizialeId = modifica ? modifica.cameraId ?? '' : creaIniziale!.cameraId ?? ''
  // Su una prenotazione esistente la Tipologia viene dal campo dedicato (può restare valorizzata
  // anche senza una camera specifica, se creata in modalità pool); in creazione, dalla camera di
  // partenza se ce n'è già una (es. click su una cella del calendario).
  const tipologiaInizialeId = modifica?.tipologiaId ?? camere.find((c) => c.id === cameraInizialeId)?.tipologiaId ?? ''

  // Nel form la Tipologia va scelta per prima: appena selezionata, la Camera si filtra a quelle di
  // quella tipologia (entrambe cercabili) — a meno che non sia attiva la modalità pool, nel qual caso
  // la Camera non si sceglie affatto: è il backend ad assegnare la prima libera al salvataggio.
  const [tipologiaFiltroId, setTipologiaFiltroId] = useState(tipologiaInizialeId)
  const [cameraId, setCameraId] = useState<string>(cameraInizialeId)
  // Riconosce che la prenotazione era stata creata in modalità pool (nessuna camera specifica, solo
  // la Tipologia) per riproporre la stessa modalità riaprendo il dialog in modifica.
  const [modalitaPool, setModalitaPool] = useState(!!modifica && !modifica.cameraId && !!modifica.tipologiaId)
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
  const [confermaAzzeraTassaAperta, setConfermaAzzeraTassaAperta] = useState(false)
  // Appena creata una nuova prenotazione, si passa direttamente alla scheda ospiti (Nome/Cognome
  // veri, non un testo libero da spezzare a indovinare) — compilabile subito o saltabile del tutto.
  const [prenotazioneAppenaCreata, setPrenotazioneAppenaCreata] = useState<PrenotazioneDto | null>(null)

  const crea = useCreaPrenotazione(strutturaId)
  const aggiorna = useAggiornaPrenotazione(strutturaId)
  const annulla = useAnnullaPrenotazione(strutturaId)
  const checkInMutation = useCheckIn(strutturaId)
  const checkOutMutation = useCheckOut(strutturaId)

  const cameraSelezionata = camere.find((c) => c.id === cameraId)
  // In modalità pool non c'è una camera scelta: la Tipologia selezionata nel filtro è l'unica fonte.
  const tipologiaSelezionata = tipologie.find((t) => t.id === (cameraSelezionata?.tipologiaId ?? tipologiaFiltroId))
  // Cauzione/Animali sono solo informazioni interne (si gestiscono di persona, non generano invii
  // esterni): se la tipologia della camera non ne prevede un importo, non ha senso mostrare il toggle.
  const cauzionePrevista = (tipologiaSelezionata?.cauzione ?? 0) > 0
  const animaliPrevisti = (tipologiaSelezionata?.animali ?? 0) > 0

  // Camere della tipologia scelta nel form — include comunque la camera già assegnata anche se non
  // corrisponde più alla tipologia selezionata, per non nascondere un'associazione esistente.
  const camereTipologia = camere.filter((c) => c.tipologiaId === tipologiaFiltroId || c.id === cameraId)
  // Conteggio "pulito" (senza il fallback sulla camera già assegnata) mostrato nell'opzione pool.
  const numeroCamereTipologia = camere.filter((c) => c.tipologiaId === tipologiaFiltroId).length
  // Con una sola camera (o zero) non c'è nulla da "scegliere automaticamente": l'opzione si mostra
  // solo quando esiste davvero un pool tra cui il sistema può selezionare.
  const mostraOpzionePool = numeroCamereTipologia > 1

  const checkInDate = checkIn ? parsaInputData(checkIn) : null
  const checkOutDate = checkOut ? parsaInputData(checkOut) : null
  const dateValide = !!checkInDate && !!checkOutDate && checkOutDate > checkInDate
  const numeroOspitiNumero = Number(numeroOspiti) || 0

  // Su una prenotazione esistente il prezzo pattuito è quello salvato (Importo totale): il
  // preventivo viene comunque ricalcolato per proporre l'aggiornamento, ma non lo sovrascrive da
  // solo — l'operatore deve confermarlo nel popup sotto (vedi propostaImporto).
  const preventivo = usePreventivo(
    strutturaId,
    cameraId || null,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    numeroOspitiNumero,
    spesePuliziaAttiva,
    animaliPrevisti && animaliAttiva,
    cauzionePrevista && cauzioneAttiva,
    true,
  )

  useEffect(() => {
    if (importoTotaleAuto && preventivo.data) {
      setImportoTotale(String(preventivo.data.totale))
    }
  }, [preventivo.data, importoTotaleAuto])

  // Snapshot dei soli campi che alimentano il preventivo — serve a poter "disfare" per intero il
  // tocco dell'operatore (checkbox, camera, date, ospiti) se rifiuta il nuovo prezzo nel popup
  // sotto, invece di lasciare uno stato incoerente (es. spunta tolta ma importo che la include ancora).
  type InputPreventivo = {
    cameraId: string
    checkIn: string
    checkOut: string
    numeroOspiti: string
    spesePuliziaAttiva: boolean
    animaliAttiva: boolean
    cauzioneAttiva: boolean
  }
  const snapshotInputPreventivo = (): InputPreventivo => ({ cameraId, checkIn, checkOut, numeroOspiti, spesePuliziaAttiva, animaliAttiva, cauzioneAttiva })

  // Su una prenotazione esistente il primo valore ricevuto è solo la "fotografia" di partenza (non
  // va proposto subito riaprendo il dialog): si propone l'aggiornamento solo quando il preventivo
  // cambia *dopo* quella fotografia, cioè in risposta a un tocco dell'operatore. I due ref restano
  // fermi sull'ultimo stato "confermato" finché l'operatore non risponde al popup (Aggiorna/Annulla),
  // non ad ogni render, altrimenti un Annulla che ripristina i campi vecchi farebbe ripartire subito
  // un secondo popup invece di richiudersi in silenzio.
  const preventivoPrecedenteRef = useRef<number | null>(null)
  const inputPrecedenteRef = useRef<InputPreventivo | null>(null)
  // "proposto" non è mai il preventivo di listino così com'è: è l'Importo totale attuale corretto
  // della sola differenza introdotta dal tocco dell'operatore. Un totale già pattuito spesso non
  // coincide col preventivo "pulito" (sconti, arrotondamenti, prezzo concordato via OTA con
  // centesimi) — sovrascriverlo col listino da zero butterebbe via quello scarto ogni volta.
  const [propostaImporto, setPropostaImporto] = useState<{ nuovoListino: number; proposto: number } | null>(null)

  useEffect(() => {
    if (!modifica || !preventivo.data) return
    const nuovoListino = preventivo.data.totale
    if (preventivoPrecedenteRef.current === null) {
      preventivoPrecedenteRef.current = nuovoListino
      inputPrecedenteRef.current = { cameraId, checkIn, checkOut, numeroOspiti, spesePuliziaAttiva, animaliAttiva, cauzioneAttiva }
      return
    }
    if (nuovoListino !== preventivoPrecedenteRef.current) {
      const delta = nuovoListino - preventivoPrecedenteRef.current
      const attuale = Number(importoTotale) || 0
      const proposto = Math.round((attuale + delta) * 100) / 100
      setPropostaImporto({ nuovoListino, proposto })
    }
  }, [preventivo.data, modifica, cameraId, checkIn, checkOut, numeroOspiti, spesePuliziaAttiva, animaliAttiva, cauzioneAttiva, importoTotale])

  function confermaAggiornaImporto() {
    if (propostaImporto === null) return
    setImportoTotale(String(propostaImporto.proposto))
    preventivoPrecedenteRef.current = propostaImporto.nuovoListino
    inputPrecedenteRef.current = snapshotInputPreventivo()
    setPropostaImporto(null)
  }

  function annullaAggiornaImporto() {
    const precedente = inputPrecedenteRef.current
    if (precedente) {
      setCameraId(precedente.cameraId)
      setCheckIn(precedente.checkIn)
      setCheckOut(precedente.checkOut)
      setNumeroOspiti(precedente.numeroOspiti)
      setSpesePuliziaAttiva(precedente.spesePuliziaAttiva)
      setAnimaliAttiva(precedente.animaliAttiva)
      setCauzioneAttiva(precedente.cauzioneAttiva)
    }
    setPropostaImporto(null)
  }

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
    if (!tipologiaFiltroId || (!modalitaPool && !cameraId) || !dateValide) {
      setErrore("Seleziona una tipologia e una camera (o l'assegnazione automatica alla prima libera) e un periodo valido (check-out dopo il check-in).")
      return
    }
    setErrore(null)

    // Disattivare la tassa azzera un importo già calcolato sulla scheda ospiti — un cambio che vale
    // soldi, non solo un flag interno: va confermato esplicitamente prima di procedere, non solo
    // eseguito silenziosamente al salvataggio.
    if (modifica && modifica.tassaSoggiornoAttiva && !tassaSoggiornoAttiva && (modifica.totalTax ?? 0) > 0) {
      setConfermaAzzeraTassaAperta(true)
      return
    }

    eseguiSalvataggio()
  }

  function eseguiSalvataggio() {
    const request: PrenotazioneRequest = {
      cameraId: modalitaPool ? null : cameraId,
      tipologiaId: tipologiaFiltroId || null,
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

  function confermaAzzeraTassa() {
    setConfermaAzzeraTassaAperta(false)
    eseguiSalvataggio()
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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
          {(!modalitaPool || !mostraOpzionePool) && (
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
          )}
        </Box>

        {/* Con una sola camera nella tipologia non c'è nessuna scelta da automatizzare: il campo
            Camera sopra la propone già da sola, l'opzione andrebbe solo a confondere. */}
        {mostraOpzionePool && !soloImporti && (
          <FormControlLabel
            control={
              <Checkbox
                checked={modalitaPool}
                onChange={(e) => {
                  setModalitaPool(e.target.checked)
                  setCameraId('')
                }}
                disabled={inCorso}
              />
            }
            label={`Assegna automaticamente la prima camera libera (${numeroCamereTipologia} camere in questa tipologia)`}
          />
        )}

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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
        {/* Una prenotazione già in corso (ospite dentro) non si annulla: si chiude con il check-out. */}
        {puoScrivere &&
          modifica &&
          modifica.statoPrenotazione !== StatoPrenotazione.Annullata &&
          modifica.statoPrenotazione !== StatoPrenotazione.Completata &&
          modifica.statoPrenotazione !== StatoPrenotazione.InCorso && (
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
        {/* Il check-in si può fare solo dal giorno dell'arrivo in poi, mai su una prenotazione futura. */}
        {puoCambiareStatoCamera &&
          modifica &&
          modifica.statoPrenotazione === StatoPrenotazione.Incompleta &&
          isOggiOPrima(modifica.checkIn) && (
            <Button variant="contained" onClick={eseguiCheckIn} disabled={inCorso}>
              Check-in
            </Button>
          )}
        {puoCambiareStatoCamera && modifica && modifica.statoPrenotazione === StatoPrenotazione.InCorso && (
          <Button variant="contained" onClick={eseguiCheckOut} disabled={inCorso}>
            Check-out
          </Button>
        )}
        {puoScrivere && (
          <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
            {modifica ? 'Salva modifiche' : 'Crea prenotazione'}
          </Button>
        )}
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

      {confermaAzzeraTassaAperta && (
        <ConfirmDialog
          titolo="Azzerare la tassa di soggiorno?"
          messaggio={`Disattivando "Tassa di soggiorno" l'importo già calcolato di €${(modifica?.totalTax ?? 0).toFixed(2)} verrà azzerato. Confermi il salvataggio?`}
          testoConferma="Salva e azzera"
          inCorso={aggiorna.isPending}
          onConferma={confermaAzzeraTassa}
          onAnnulla={() => setConfermaAzzeraTassaAperta(false)}
        />
      )}

      {propostaImporto !== null && (
        <ConfirmDialog
          titolo="Aggiornare l'importo totale?"
          messaggio={`In base alla modifica appena fatta l'Importo totale corretto sarebbe €${propostaImporto.proposto.toFixed(2)} (attuale: €${importoTotale || '0'}). Vuoi aggiornarlo? Annullando, la modifica appena fatta viene ripristinata com'era.`}
          testoConferma="Aggiorna"
          pericoloso={false}
          onConferma={confermaAggiornaImporto}
          onAnnulla={annullaAggiornaImporto}
        />
      )}
    </Dialog>
  )
}
