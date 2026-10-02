import { useEffect, useMemo, useRef, useState } from 'react'
import { ConfermaSchedinaSoggiornoBreve, isSoggiornoBreve } from './ConfermaSchedinaSoggiornoBreve'
import { ProponiFatturaDopoCheckIn } from './ProponiFatturaDopoCheckIn'
import { useAggiornaRinunceServizi } from '../api/pulizie'
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
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import CloseIcon from '@mui/icons-material/CloseOutlined'
import TextField from '@mui/material/TextField'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import type { CameraDto } from '../api/camere'
import type { CanaleVenditaDto } from '../api/canaliVendita'
import type { TipologiaCameraDto } from '../api/tipologie'
import {
  useAggiornaPrenotazione,
  useAnnullaPrenotazione,
  useCheckIn,
  useCreaPrenotazione,
  usePreventivo,
  useCameraAssegnabile,
  useVerificaDisponibilita,
  StatoPrenotazione,
  type DisponibilitaCameraDto,
  type PrenotazioneDto,
  type PrenotazioneRequest,
} from '../api/prenotazioni'
import { ApiError } from '../api/client'
import { nomeDocumento, testoDaFatturare, useDaFatturare, useFatturaPerPrenotazione } from '../api/fatturazione'
import { FatturaDialog } from './FatturaDialog'
import { NOME_TRATTAMENTO, TipoTrattamento, scaricaBuoniColazione, useTrattamenti } from '../api/trattamenti'
import { TipoPagamento, nettoPagamenti, usePagamenti, type SalvaPagamentoRequest } from '../api/pagamenti'
import { PagamentiPrenotazione } from './PagamentiPrenotazione'
import { CheckOutDialog } from './CheckOutDialog'
import {
  NOME_MODALITA,
  OrigineServizio,
  importoRiga,
  nottiTra,
  perNotte,
  perPersona,
  useServizi,
  useServiziPrenotazione,
  type ModalitaPrezzoServizio,
} from '../api/servizi'
import { aggiungiGiorni, formatoInputData, inizioGiornoLocale, isOggiOPrima, isoLocale, parsaInputData, perCampoDataOra } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { tokens } from '../theme'
import { OspiteDialog } from './OspiteDialog'
import { ConfirmDialog } from './ConfirmDialog'
import { usePuoScrivere } from '../permessi/usePuoScrivere'
import { useStruttura } from '../struttura/StrutturaContext'

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

/**
 * Età di un bambino digitata a mano: solo cifre, e oltre 17 diventa 17, come fanno le frecce. Il max
 * del campo ferma solo le frecce, non la tastiera. Null = tasto da ignorare (segno meno, decimali).
 */
function etaBambinoDigitata(valore: string): string | null {
  if (valore === '') return ''
  if (!/^\d+$/.test(valore)) return null
  return String(Math.min(Number(valore), 17))
}

function messaggioConflitto(conflitto: DisponibilitaCameraDto): string {
  const dal = conflitto.checkIn ? formattatoreData.format(new Date(conflitto.checkIn)) : '?'
  const al = conflitto.checkOut ? formattatoreData.format(new Date(conflitto.checkOut)) : '?'
  return `Questa camera è già prenotata dal ${dal} al ${al}.`
}

/**
 * Servizio extra sulla prenotazione nel form: quantità e date sono testo finché si scrivono (date
 * "YYYY-MM-DD"). `al` solo per i servizi a notte, come il check-out. `rigaId` null = riga nuova.
 */
interface RigaServizio {
  chiave: string
  rigaId: string | null
  servizioId: string
  nome: string
  modalita: ModalitaPrezzoServizio
  prezzoUnitario: number
  quantita: string
  dal: string
  al: string
  origine: OrigineServizio
  aggiuntoDa: string | null
  aggiuntoIlUtc: string | null
}

const formattatoreGiornoMese = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit' })

const formattatoreEuro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattaEuro = (valore: number) => formattatoreEuro.format(valore)

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
  const puoFareCheckInOut = usePuoScrivere('checkInOut')
  const puoFatturare = usePuoScrivere('financeWrite')
  // Annullare qui una prenotazione dell'OTA (quando l'annullamento dell'OTA non arriva): titolare o
  // chi gestisce gli utenti della struttura, non il Super Admin. Il backend controlla lo stesso.
  const { isSuperAdmin } = useStruttura()
  const puoGestireUtenti = usePuoScrivere('settingUser')
  const puoAnnullareOta = puoGestireUtenti && !isSuperAdmin
  const modifica = stato.modo === 'modifica' ? stato.prenotazione : null
  // Chi non vede le finanze riceve null (403), e l'indicazione semplicemente non compare.
  const fatturaGenerata = useFatturaPerPrenotazione(strutturaId, modifica?.id ?? null).data ?? null
  const extraDaFatturare = testoDaFatturare(useDaFatturare(fatturaGenerata ? strutturaId : null, modifica?.id ?? null).data)
  const [fatturaExtraAperta, setFatturaExtraAperta] = useState(false)
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
  // Adulti e bambini separati, come su qualunque motore di prenotazione: con un solo "Numero ospiti"
  // chi scriveva gli adulti e poi aggiungeva i bambini li contava due volte come posti e sbagliava il
  // prezzo. In `NumeroOspiti` resta il totale, quindi gli adulti di una prenotazione salvata sono la
  // differenza (almeno uno).
  const [adulti, setAdulti] = useState<string>(
    String(modifica ? Math.max((modifica.numeroOspiti ?? 2) - (modifica.etaBambini?.length ?? 0), 1) : 2),
  )
  // Età all'arrivo di ciascun bambino, una per campo: il supplemento per persona in più può
  // dipendere dall'età (fasce della tipologia).
  const [etaBambini, setEtaBambini] = useState<string[]>(modifica?.etaBambini?.map(String) ?? [])
  // Null = solo pernottamento.
  const [trattamento, setTrattamento] = useState<TipoTrattamento | null>(modifica?.trattamento ?? null)
  const trattamenti = useTrattamenti(strutturaId)
  // Quelli offerti, più quello già sulla prenotazione anche se nel frattempo non è più offerto:
  // sparirebbe dalla scelta e un salvataggio qualsiasi lo toglierebbe.
  const trattamentiSceglibili = (Object.values(TipoTrattamento) as TipoTrattamento[]).filter(
    (t) => (trattamenti.data ?? []).some((l) => l.tipo === t && l.attivo) || t === modifica?.trattamento,
  )
  // Servizi extra (escursioni, parcheggio…): più d'uno per prenotazione. In modifica si leggono a parte
  // (le liste del calendario non li portano) e finché non sono arrivati valgono null: il preventivo
  // aspetta e il salvataggio li lascia come sono, invece di cancellarli.
  const servizi = useServizi(strutturaId)
  const serviziPrenotazione = useServiziPrenotazione(strutturaId, modifica?.id ?? null)
  const [righeServiziModificate, setRigheServiziModificate] = useState<RigaServizio[] | null>(null)
  const [ricercaServizio, setRicercaServizio] = useState('')
  const serviziSalvati = serviziPrenotazione.data
  const righeServizi = useMemo<RigaServizio[] | null>(
    () =>
      righeServiziModificate ??
      (!modifica
        ? []
        : (serviziSalvati?.map((r) => ({
            chiave: r.id,
            rigaId: r.id,
            servizioId: r.servizioId,
            nome: r.nome,
            modalita: r.modalita,
            prezzoUnitario: r.prezzoUnitario,
            quantita: String(r.quantita),
            dal: r.dal,
            al: r.al ?? '',
            origine: r.origine,
            aggiuntoDa: r.aggiuntoDa,
            aggiuntoIlUtc: r.aggiuntoIlUtc,
          })) ?? null)),
    [righeServiziModificate, modifica, serviziSalvati],
  )
  // Chiave per le righe nuove, che non hanno ancora un id: lo stesso servizio può comparire più volte.
  const prossimaRigaNuova = useRef(1)
  // Date della riga complete e dentro il soggiorno (le stesse regole del backend): solo queste vanno al
  // preventivo, le altre fermano il salvataggio con un messaggio invece di un errore del server.
  const rigaValida = (r: RigaServizio) =>
    Number.isInteger(Number(r.quantita)) &&
    Number(r.quantita) >= 1 &&
    r.dal !== '' &&
    r.dal >= checkIn &&
    (perNotte(r.modalita) ? r.al !== '' && r.al > r.dal && r.al <= checkOut : r.dal <= checkOut)
  const richiesteServizi = (righeServizi ?? []).filter(rigaValida).map((r) => ({
    rigaId: r.rigaId,
    servizioId: r.servizioId,
    quantita: Number(r.quantita),
    dal: r.dal,
    al: perNotte(r.modalita) ? r.al : null,
  }))
  // Tutti gli offerti: lo stesso servizio si può aggiungere di nuovo per un altro giorno.
  const serviziSceglibili = (servizi.data ?? []).filter((s) => s.attivo)
  // Dopo il check-in, e anche dopo il check-out, quello che si aggiunge è un addebito sul conto.
  const soggiornoInCorso = modifica?.statoPrenotazione === StatoPrenotazione.InCorso || modifica?.statoPrenotazione === StatoPrenotazione.Completata

  function aggiornaRiga(chiave: string, modifiche: Partial<RigaServizio>) {
    if (!righeServizi) return
    setRigheServiziModificate(righeServizi.map((r) => (r.chiave === chiave ? { ...r, ...modifiche } : r)))
  }

  // Una riga a notte che copriva tutto il soggiorno lo segue quando cambiano le date: è il caso comune
  // (parcheggio per tutta la permanenza). Quelle su notti scelte restano, e se escono dal soggiorno lo
  // dice il controllo al salvataggio.
  function seguiSoggiorno(nuovoCheckIn: string, nuovoCheckOut: string) {
    if (!righeServizi?.some((r) => perNotte(r.modalita) && r.dal === checkIn && r.al === checkOut)) return
    setRigheServiziModificate(
      righeServizi.map((r) => (perNotte(r.modalita) && r.dal === checkIn && r.al === checkOut ? { ...r, dal: nuovoCheckIn, al: nuovoCheckOut } : r)),
    )
  }
  const [importoTotale, setImportoTotale] = useState<string>(modifica?.importoTotale != null ? String(modifica.importoTotale) : '')
  // Il tab dei servizi c'è solo se c'è qualcosa da vendere: un trattamento o un servizio offerto, oppure
  // quelli che la prenotazione ha già (anche se nel frattempo non sono più offerti).
  const mostraTabServizi =
    trattamentiSceglibili.length > 0 || (servizi.data ?? []).some((x) => x.attivo) || (righeServizi?.length ?? 0) > 0
  const [tab, setTab] = useState<'soggiorno' | 'servizi' | 'pagamenti'>('soggiorno')
  const tabEffettivo = tab === 'servizi' && !mostraTabServizi ? 'soggiorno' : tab
  // Pagamenti: in creazione restano qui e partono con la prenotazione; su una salvata si registrano
  // subito, e il pagato è la loro somma (non si scrive più a mano).
  const [pagamentiLocali, setPagamentiLocali] = useState<SalvaPagamentoRequest[]>([])
  const pagamentiSalvati = usePagamenti(strutturaId, modifica?.id ?? null)
  const pagato = modifica
    ? pagamentiSalvati.data
      ? nettoPagamenti(pagamentiSalvati.data)
      : (modifica.importoPagato ?? 0)
    : nettoPagamenti(pagamentiLocali)
  const numeroPagamenti = modifica ? (pagamentiSalvati.data?.length ?? 0) : pagamentiLocali.length
  // Finché l'operatore non tocca il campo a mano, "Importo totale" segue il preventivo — se cambi
  // camera/date/checkbox si aggiorna da solo, senza dover recliccare "Usa" ogni volta.
  const [importoTotaleAuto, setImportoTotaleAuto] = useState(!modifica)
  // La sezione (e questo checkbox) compare solo quando la cauzione si applica davvero a questa
  // prenotazione (tipologia con importo configurato E toggle "Cauzione" attivo) — quando è
  // mostrata, il default sensato è restituirla per intero.
  const [checkOutAperto, setCheckOutAperto] = useState(false)
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
  const [schedinaBreveDaInviare, setSchedinaBreveDaInviare] = useState(false)
  // Dopo il check-in si propone la fattura (vedi ProponiFatturaDopoCheckIn); se c'è anche la domanda
  // sulla schedina del soggiorno breve, viene dopo quella. Il dialog si chiude solo alla fine.
  const [fatturaDaProporre, setFatturaDaProporre] = useState<PrenotazioneDto | null>(null)
  const [fatturaInAttesa, setFatturaInAttesa] = useState<PrenotazioneDto | null>(null)
  // Orario reale dell'arrivo: si corregge solo su un soggiorno già iniziato, e serve a rimettere a
  // posto i termini della schedina alloggiati quando il check-in è stato registrato in ritardo.
  // Il campo datetime-local lavora in ora locale, il backend in UTC: conversione in entrambi i sensi.
  const [arrivoEffettivo, setArrivoEffettivo] = useState(
    modifica?.checkInEffettuatoAtUtc ? perCampoDataOra(modifica.checkInEffettuatoAtUtc) : '',
  )
  // Rinunce dell'ospite a pulizia e cambio biancheria: si salvano subito con un endpoint loro (non con
  // "Salva modifiche"), perché si registrano quando l'ospite lo dice, spesso a soggiorno in corso.
  const aggiornaRinunce = useAggiornaRinunceServizi(strutturaId)
  const [rinunce, setRinunce] = useState({
    rinunciaPulizia: modifica?.rinunciaPulizia ?? false,
    rinunciaBiancheria: modifica?.rinunciaBiancheria ?? false,
  })
  function salvaRinunce(nuove: { rinunciaPulizia: boolean; rinunciaBiancheria: boolean }) {
    if (!modifica) return
    const precedenti = rinunce
    setRinunce(nuove)
    aggiornaRinunce.mutate(
      { prenotazioneId: modifica.id, ...nuove },
      {
        onError: (err) => {
          setRinunce(precedenti)
          gestisciErrore(err)
        },
      },
    )
  }

  const cameraSelezionata = camere.find((c) => c.id === cameraId)
  // In modalità pool non c'è una camera scelta: la Tipologia selezionata nel filtro è l'unica fonte.
  const tipologiaSelezionata = tipologie.find((t) => t.id === (cameraSelezionata?.tipologiaId ?? tipologiaFiltroId))
  // Cauzione/Animali sono solo informazioni interne (si gestiscono di persona, non generano invii
  // esterni): se la tipologia della camera non ne prevede un importo, non ha senso mostrare il toggle.
  const cauzionePrevista = (tipologiaSelezionata?.cauzione ?? 0) > 0
  const animaliPrevisti = (tipologiaSelezionata?.animali ?? 0) > 0
  // Animale già sulla prenotazione (es. dichiarato sul sito web) anche senza supplemento nella
  // tipologia: la spunta si mostra comunque, e salvando non si perde.
  const mostraAnimali = animaliPrevisti || !!modifica?.animaliAttiva

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
  // Solo le età già scritte e valide entrano nel preventivo: un campo appena aggiunto e ancora vuoto
  // non deve far fallire il calcolo. Al salvataggio decide il backend.
  const etaBambiniValide = etaBambini
    .filter((e) => e.trim() !== '')
    .map(Number)
    .filter((e) => Number.isInteger(e) && e >= 0 && e <= 17)
  const adultiNumero = Number(adulti) || 0
  // Un campo ancora vuoto non conta: al salvataggio si chiede di compilarlo o toglierlo.
  const numeroOspitiNumero = adultiNumero + etaBambiniValide.length

  // Con l'assegnazione automatica la camera la sceglie il backend al salvataggio: qui si chiede quale
  // sceglierebbe adesso, per calcolare il preventivo su quella (il prezzo può essere per camera) e
  // avvisare subito se la tipologia è piena, invece di scoprirlo premendo "Crea".
  const cameraAssegnabile = useCameraAssegnabile(
    strutturaId,
    tipologiaFiltroId || null,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    modifica?.id ?? null,
    modalitaPool && mostraOpzionePool && dateValide,
  )
  const cameraPreventivoId = modalitaPool ? (cameraAssegnabile.data?.cameraId ?? null) : cameraId || null
  const tipologiaPiena = modalitaPool && cameraAssegnabile.data !== undefined && cameraAssegnabile.data.cameraId === null

  // Su una prenotazione esistente il prezzo pattuito è quello salvato (Importo totale): il
  // preventivo viene comunque ricalcolato per proporre l'aggiornamento, ma non lo sovrascrive da
  // solo — l'operatore deve confermarlo nel popup sotto (vedi propostaImporto).
  const preventivo = usePreventivo(
    strutturaId,
    cameraPreventivoId,
    dateValide ? isoLocale(checkInDate!) : null,
    dateValide ? isoLocale(checkOutDate!) : null,
    numeroOspitiNumero,
    etaBambiniValide,
    spesePuliziaAttiva,
    animaliPrevisti && animaliAttiva,
    cauzionePrevista && cauzioneAttiva,
    righeServizi !== null,
    trattamento,
    modifica?.id ?? null,
    richiesteServizi,
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
    adulti: string
    etaBambini: string[]
    trattamento: TipoTrattamento | null
    servizi: RigaServizio[] | null
    spesePuliziaAttiva: boolean
    animaliAttiva: boolean
    cauzioneAttiva: boolean
  }
  const snapshotInputPreventivo = (): InputPreventivo => ({
    cameraId,
    checkIn,
    checkOut,
    adulti,
    etaBambini,
    trattamento,
    servizi: righeServizi,
    spesePuliziaAttiva,
    animaliAttiva,
    cauzioneAttiva,
  })

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
      inputPrecedenteRef.current = { cameraId, checkIn, checkOut, adulti, etaBambini, trattamento, servizi: righeServizi, spesePuliziaAttiva, animaliAttiva, cauzioneAttiva }
      return
    }
    if (nuovoListino !== preventivoPrecedenteRef.current) {
      const delta = nuovoListino - preventivoPrecedenteRef.current
      const attuale = Number(importoTotale) || 0
      const proposto = Math.round((attuale + delta) * 100) / 100
      setPropostaImporto({ nuovoListino, proposto })
    }
  }, [preventivo.data, modifica, cameraId, checkIn, checkOut, adulti, etaBambini, trattamento, righeServizi, spesePuliziaAttiva, animaliAttiva, cauzioneAttiva, importoTotale])

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
      setAdulti(precedente.adulti)
      setEtaBambini(precedente.etaBambini)
      setTrattamento(precedente.trattamento)
      setRigheServiziModificate(precedente.servizi)
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

  // Per "Diretta" il numero lo assegna il sistema: in creazione non c'è ancora nulla da mostrare
  // e il campo resta nascosto, mentre su una prenotazione già salvata si vede in sola lettura —
  // è il riferimento che si detta all'ospite, e prima andava cercato altrove. Una volta assegnato
  // non cambia più (nemmeno il backend lo rigenera); un valore digitato prima di scegliere Diretta
  // viene invece scartato, per non spacciarlo per scelto a mano.
  const numeroAssegnato = modifica?.numeroPrenotazione ?? ''
  useEffect(() => {
    if (agenzia === 'Diretta' && numeroPrenotazione !== numeroAssegnato) {
      setNumeroPrenotazione(numeroAssegnato)
    }
  }, [agenzia, numeroPrenotazione, numeroAssegnato])

  const inCorso = crea.isPending || aggiorna.isPending || annulla.isPending || checkInMutation.isPending
  // Un soggiorno Completato è chiuso: resta modificabile solo il saldo (Importo totale/Importo
  // pagato), tutto il resto (camera, date, toggle...) è quello con cui si è effettivamente svolto.
  const soloImporti = modifica?.statoPrenotazione === StatoPrenotazione.Completata

  function gestisciErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  async function scaricaBuoni() {
    if (!modifica) return
    try {
      await scaricaBuoniColazione(strutturaId, modifica.id, modifica.numeroPrenotazione)
    } catch (err) {
      gestisciErrore(err)
    }
  }

  function salva() {
    if (!tipologiaFiltroId || (!modalitaPool && !cameraId) || !dateValide) {
      setTab('soggiorno')
      setErrore("Seleziona una tipologia e una camera (o l'assegnazione automatica alla prima libera) e un periodo valido (check-out dopo il check-in).")
      return
    }
    if (adultiNumero < 1) {
      setTab('soggiorno')
      setErrore('Serve almeno un adulto.')
      return
    }
    // Un bambino senza età non si scarta in silenzio: cambierebbe il numero degli ospiti.
    if (etaBambini.some((e) => e.trim() === '')) {
      setTab('soggiorno')
      setErrore("Indica l'età di ogni bambino, oppure toglilo.")
      return
    }
    if ((righeServizi ?? []).length !== richiesteServizi.length) {
      setTab('servizi')
      setErrore('Controlla i servizi extra: quantità da 1 a 99 e date dentro il soggiorno (per quelli a notte almeno una notte).')
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
      // Il pagato è la somma del registro pagamenti: il backend non lo prende più da qui.
      importoPagato: null,
      pagamenti: modifica ? null : pagamentiLocali,
      importoTotale: importoTotale.trim() === '' ? null : Number(importoTotale),
      checkIn: isoLocale(checkInDate!),
      checkOut: isoLocale(checkOutDate!),
      numeroOspiti: numeroOspitiNumero,
      etaBambini: etaBambiniValide,
      trattamento,
      servizi: righeServizi === null ? null : richiesteServizi,
      tassaSoggiornoAttiva,
      spesePuliziaAttiva,
      animaliAttiva: mostraAnimali && animaliAttiva,
      cauzioneAttiva: cauzionePrevista && cauzioneAttiva,
      // Solo se valorizzato: null lascia intatto l'orario registrato al check-in, così un
      // salvataggio qualunque non lo cancella.
      checkInEffettuatoAtUtc: arrivoEffettivo ? new Date(arrivoEffettivo).toISOString() : null,
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
    // Annullata solo qui resterebbe venduta sul portale: si annulla dall'OTA (il backend lo rifiuta comunque).
    // Chi può farlo lo stesso passa dalla conferma, con un avviso.
    if (modifica.daOta && !puoAnnullareOta) {
      setErrore("Questa prenotazione è arrivata dall'OTA: va annullata dall'OTA. L'annullamento arriverà qui in automatico.")
      return
    }
    setConfermaAnnullaAperta(true)
  }

  function confermaAnnulla() {
    if (!modifica) return
    annulla.mutate({ prenotazioneId: modifica.id, anchePerOta: !!modifica.daOta }, {
      onSuccess: onClose,
      onError: (err) => {
        setConfermaAnnullaAperta(false)
        gestisciErrore(err)
      },
    })
  }

  function eseguiCheckIn() {
    if (!modifica) return
    checkInMutation.mutate(modifica.id, {
      // Per un soggiorno sotto le 24 ore la schedina ha 6 ore di tempo: si chiede subito se
      // trasmetterla, e il dialog si chiude solo dopo la risposta (altrimenti la domanda sparirebbe
      // insieme alla schermata).
      onSuccess: (aggiornata) => {
        const daFatturare = puoFatturare ? aggiornata : null
        if (isSoggiornoBreve(modifica.checkIn, modifica.checkOut)) {
          setSchedinaBreveDaInviare(true)
          setFatturaInAttesa(daFatturare)
          return
        }
        if (daFatturare) {
          setFatturaDaProporre(daFatturare)
          return
        }
        onClose()
      },
      onError: gestisciErrore,
    })
  }

  function eseguiCheckOut() {
    if (!modifica) return
    setCheckOutAperto(true)
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
        {fatturaGenerata && (
          <Chip
            size="small"
            label={extraDaFatturare ? `${nomeDocumento(fatturaGenerata)} — ${extraDaFatturare}` : nomeDocumento(fatturaGenerata)}
            sx={{ bgcolor: extraDaFatturare ? tokens.wait600 : tokens.ok600, color: '#fff', fontWeight: 700 }}
          />
        )}
      </DialogTitle>

      <Tabs
        value={tabEffettivo}
        onChange={(_, v) => setTab(v)}
        variant="scrollable"
        scrollButtons="auto"
        allowScrollButtonsMobile
        sx={{ px: 3, minHeight: 0, borderBottom: `1px solid ${tokens.surfaceBorder}`, '& .MuiTab-root': { pt: 1, pb: 1.75 } }}
      >
        <Tab label="Soggiorno" value="soggiorno" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        {mostraTabServizi && (
          <Tab
            label={`Trattamento e servizi${righeServizi && righeServizi.length > 0 ? ` (${righeServizi.length})` : ''}`}
            value="servizi"
            sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }}
          />
        )}
        <Tab label={`Pagamenti${numeroPagamenti > 0 ? ` (${numeroPagamenti})` : ''}`} value="pagamenti" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
      </Tabs>

      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2.5 }}>
        {/* Il Box (invece di renderizzare {errore && ...} nudo) garantisce che il campo Tipologia
            sotto non sia mai il primo figlio letterale del contenitore flex quando non c'è errore:
            un Autocomplete in quella posizione esatta mostra la label ristretta tagliata a metà dal
            bordo (bug reale di rendering riprodotto e isolato, non specifico di un singolo campo). */}
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        {tabEffettivo === 'pagamenti' && (
          <PagamentiPrenotazione
            strutturaId={strutturaId}
            prenotazioneId={modifica?.id ?? null}
            locali={pagamentiLocali}
            onCambiaLocali={setPagamentiLocali}
            daSaldare={Math.max(Math.round(((Number(importoTotale) || 0) - pagato) * 100) / 100, 0)}
            tipoSuggerito={
              modifica && (modifica.statoPrenotazione === StatoPrenotazione.InCorso || modifica.statoPrenotazione === StatoPrenotazione.Completata)
                ? TipoPagamento.Saldo
                : TipoPagamento.Acconto
            }
            puoScrivere={puoScrivere}
            disabled={inCorso}
          />
        )}

        {tabEffettivo === 'servizi' && (
          <>
            {/* Il tab c'è solo se la struttura offre qualcosa, o se la prenotazione ne ha già. */}
            {trattamentiSceglibili.length > 0 && (
              <TextField
                select
                label="Trattamento"
                value={trattamento ?? ''}
                onChange={(e) => setTrattamento(e.target.value === '' ? null : (Number(e.target.value) as TipoTrattamento))}
                disabled={inCorso || soloImporti}
                helperText="A persona e a notte, in aggiunta al prezzo della camera."
                // Senza displayEmpty la voce con valore vuoto ("Solo pernottamento") non si vede una volta scelta.
                slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
              >
                <MenuItem value="">Solo pernottamento</MenuItem>
                {trattamentiSceglibili.map((t) => (
                  <MenuItem key={t} value={t}>
                    {NOME_TRATTAMENTO[t]}
                  </MenuItem>
                ))}
              </TextField>
            )}

            {righeServizi !== null && (serviziSceglibili.length > 0 || righeServizi.length > 0) && (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
                  Servizi extra: si sommano all&apos;importo come il trattamento. Il prezzo resta quello del momento in cui sono stati aggiunti.
                </Typography>
                {/* Dopo il check-in quello che si aggiunge è un addebito sul conto: i due gruppi restano separati. */}
                {[
                  { titolo: 'Con la prenotazione', righe: righeServizi.filter((r) => r.origine === OrigineServizio.ConLaPrenotazione) },
                  { titolo: 'Durante il soggiorno', righe: righeServizi.filter((r) => r.origine === OrigineServizio.DuranteIlSoggiorno) },
                ]
                  .filter((g) => g.righe.length > 0)
                  .map((gruppo) => (
                    <Box key={gruppo.titolo} sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                      {righeServizi.some((r) => r.origine === OrigineServizio.DuranteIlSoggiorno) && (
                        <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>{gruppo.titolo}</Typography>
                      )}
                      {gruppo.righe.map((r) => {
                        const aNotte = perNotte(r.modalita)
                        const notti = aNotte && r.dal !== '' && r.al !== '' ? nottiTra(r.dal, r.al) : 0
                        const quantita = Number(r.quantita)
                        return (
                          <Box
                            key={r.chiave}
                            sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, p: 1.5, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5 }}
                          >
                            <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                              <Box sx={{ flex: 1, minWidth: 0 }}>
                                <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>{r.nome}</Typography>
                                <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
                                  {formattaEuro(r.prezzoUnitario)} {NOME_MODALITA[r.modalita]}
                                  {rigaValida(r) && ` · ${formattaEuro(importoRiga(r.modalita, r.prezzoUnitario, quantita, notti))}`}
                                  {aNotte && notti > 0 && ` (${notti} ${notti === 1 ? 'notte' : 'notti'})`}
                                  {r.aggiuntoDa &&
                                    ` · aggiunto da ${r.aggiuntoDa}${r.aggiuntoIlUtc ? ` il ${formattatoreGiornoMese.format(new Date(r.aggiuntoIlUtc))}` : ''}`}
                                </Typography>
                              </Box>
                              <IconButton
                                size="small"
                                aria-label={`Togli ${r.nome}`}
                                onClick={() => setRigheServiziModificate(righeServizi.filter((x) => x.chiave !== r.chiave))}
                                disabled={inCorso}
                              >
                                <CloseIcon fontSize="small" />
                              </IconButton>
                            </Box>
                            <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'flex-start' }}>
                              {aNotte ? (
                                <>
                                  <Box sx={{ width: mobile ? '100%' : 170 }}>
                                    <CampoData
                                      label="Dal"
                                      size="small"
                                      value={r.dal}
                                      onChange={(v) => aggiornaRiga(r.chiave, { dal: v })}
                                      min={checkIn}
                                      max={checkOut ? formatoInputData(aggiungiGiorni(parsaInputData(checkOut), -1)) : undefined}
                                      fullWidth
                                      disabled={inCorso}
                                    />
                                  </Box>
                                  <Box sx={{ width: mobile ? '100%' : 170 }}>
                                    <CampoData
                                      label="Al"
                                      size="small"
                                      value={r.al}
                                      onChange={(v) => aggiornaRiga(r.chiave, { al: v })}
                                      min={r.dal ? formatoInputData(aggiungiGiorni(parsaInputData(r.dal), 1)) : checkIn}
                                      max={checkOut}
                                      fullWidth
                                      disabled={inCorso}
                                    />
                                  </Box>
                                </>
                              ) : (
                                <Box sx={{ width: mobile ? '100%' : 170 }}>
                                  <CampoData
                                    label="Data"
                                    size="small"
                                    value={r.dal}
                                    onChange={(v) => aggiornaRiga(r.chiave, { dal: v })}
                                    min={checkIn}
                                    max={checkOut}
                                    fullWidth
                                    disabled={inCorso}
                                  />
                                </Box>
                              )}
                              <TextField
                                label={perPersona(r.modalita) ? 'Persone' : 'Quantità'}
                                type="number"
                                size="small"
                                value={r.quantita}
                                onChange={(e) => {
                                  const v = e.target.value
                                  if (v !== '' && !/^\d+$/.test(v)) return
                                  aggiornaRiga(r.chiave, { quantita: v === '' ? '' : String(Math.min(Number(v), 99)) })
                                }}
                                disabled={inCorso}
                                slotProps={{ htmlInput: { min: 1, max: 99 } }}
                                sx={{ width: 110 }}
                              />
                            </Box>
                          </Box>
                        )
                      })}
                    </Box>
                  ))}
                {serviziSceglibili.length > 0 && (
                  <Autocomplete
                    size="small"
                    options={serviziSceglibili}
                    getOptionLabel={(x) => x.nome}
                    // Sempre vuoto: scegliere un servizio lo aggiunge come riga sopra, il campo torna libero.
                    value={null}
                    inputValue={ricercaServizio}
                    onInputChange={(_, v, motivo) => setRicercaServizio(motivo === 'reset' ? '' : v)}
                    blurOnSelect
                    onChange={(_, scelto) => {
                      if (!scelto) return
                      setRicercaServizio('')
                      // A notte: tutto il soggiorno. Gli altri: il giorno d'arrivo, oppure oggi se l'ospite è
                      // già in struttura (è un addebito del momento, come la SPA prenotata alla reception).
                      const oggi = formatoInputData(new Date())
                      const giorno = soggiornoInCorso && oggi >= checkIn && oggi <= checkOut ? oggi : checkIn
                      setRigheServiziModificate([
                        ...righeServizi,
                        {
                          chiave: `nuova-${prossimaRigaNuova.current++}`,
                          rigaId: null,
                          servizioId: scelto.id,
                          nome: scelto.nome,
                          modalita: scelto.modalita,
                          prezzoUnitario: scelto.prezzo,
                          // A persona: di norma tutti gli ospiti, poi si corregge (un'escursione per due su quattro).
                          quantita: String(perPersona(scelto.modalita) ? Math.max(numeroOspitiNumero, 1) : 1),
                          dal: perNotte(scelto.modalita) ? checkIn : giorno,
                          al: perNotte(scelto.modalita) ? checkOut : '',
                          origine: soggiornoInCorso ? OrigineServizio.DuranteIlSoggiorno : OrigineServizio.ConLaPrenotazione,
                          aggiuntoDa: null,
                          aggiuntoIlUtc: null,
                        },
                      ])
                    }}
                    disabled={inCorso}
                    noOptionsText="Nessun servizio con questo nome"
                    renderOption={({ key, ...props }, x) => (
                      <li key={key} {...props}>
                        {x.nome} — {formattaEuro(x.prezzo)} {NOME_MODALITA[x.modalita]}
                      </li>
                    )}
                    renderInput={(params) => <TextField {...params} label="Aggiungi servizio extra" placeholder="Cerca per nome…" />}
                  />
                )}
              </Box>
            )}

          </>
        )}

        {tabEffettivo === 'soggiorno' && (
          <>
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
            {modalitaPool && mostraOpzionePool && !soloImporti && tipologiaPiena && (
              <Alert severity="warning">Nessuna camera libera di questa tipologia per le date scelte.</Alert>
            )}
            {modalitaPool && mostraOpzionePool && !soloImporti && cameraAssegnabile.data?.nomeCamera && (
              <Typography sx={{ fontSize: 12, color: tokens.textTertiary, mt: -1.5 }}>
                Verrebbe assegnata la camera {cameraAssegnabile.data.nomeCamera}: la scelta definitiva si fa al salvataggio.
              </Typography>
            )}

            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <CampoData
                label="Check-in"
                value={checkIn}
                onChange={(v) => {
                  seguiSoggiorno(v, checkOut)
                  setCheckIn(v)
                }}
                fullWidth
                disabled={inCorso || soloImporti}
              />
              <CampoData
                label="Check-out"
                value={checkOut}
                onChange={(v) => {
                  seguiSoggiorno(checkIn, v)
                  setCheckOut(v)
                }}
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
                label="Adulti"
                type="number"
                value={adulti}
                onChange={(e) => setAdulti(e.target.value)}
                fullWidth
                disabled={inCorso || soloImporti}
                helperText={etaBambiniValide.length > 0 ? `Ospiti in tutto: ${numeroOspitiNumero}` : ' '}
                slotProps={{ htmlInput: { min: 1 } }}
              />
            </Box>

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
                Bambini, con l&apos;età all&apos;arrivo: si aggiungono agli adulti, e il supplemento per persona in più può cambiare con l&apos;età.
              </Typography>
              <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.5, alignItems: 'center' }}>
                {etaBambini.map((eta, i) => (
                  <TextField
                    key={i}
                    label={`Età bambino ${i + 1}`}
                    type="number"
                    size="small"
                    value={eta}
                    onChange={(e) => {
                      const valore = etaBambinoDigitata(e.target.value)
                      if (valore !== null) setEtaBambini(etaBambini.map((v, j) => (j === i ? valore : v)))
                    }}
                    disabled={inCorso || soloImporti}
                    sx={{ width: 150 }}
                    slotProps={{
                      htmlInput: { min: 0, max: 17 },
                      // Sempre ristretta: a campo vuoto l'etichetta finiva sotto la X per togliere il bambino.
                      inputLabel: { shrink: true },
                      input: {
                        endAdornment: (
                          <InputAdornment position="end">
                            <IconButton
                              size="small"
                              edge="end"
                              aria-label={`Togli bambino ${i + 1}`}
                              onClick={() => setEtaBambini(etaBambini.filter((_, j) => j !== i))}
                              disabled={inCorso || soloImporti}
                            >
                              <CloseIcon fontSize="small" />
                            </IconButton>
                          </InputAdornment>
                        ),
                      },
                    }}
                  />
                ))}
                <Button size="small" onClick={() => setEtaBambini([...etaBambini, ''])} disabled={inCorso || soloImporti}>
                  Aggiungi bambino
                </Button>
              </Box>
            </Box>

            {/* Quello che il portale ha mandato oltre ai dati standard: si aggiorna solo dall'OTA. */}
            {modifica?.noteOta && (
              <Alert severity="info" icon={false}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, mb: 0.5 }}>Indicazioni dall&apos;OTA</Typography>
                <Typography sx={{ fontSize: 13, whiteSpace: 'pre-line', overflowWrap: 'anywhere' }}>{modifica.noteOta}</Typography>
              </Alert>
            )}

            {(agenzia !== 'Diretta' || numeroAssegnato !== '') && (
              <TextField
                label="Numero prenotazione"
                value={numeroPrenotazione}
                onChange={(e) => setNumeroPrenotazione(e.target.value)}
                disabled={inCorso || soloImporti}
                slotProps={{ input: { readOnly: agenzia === 'Diretta' } }}
                helperText={agenzia === 'Diretta' ? 'Assegnato automaticamente dal sistema' : undefined}
              />
            )}

            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              <FormControlLabel
                control={<Checkbox checked={spesePuliziaAttiva} onChange={(e) => setSpesePuliziaAttiva(e.target.checked)} disabled={inCorso || soloImporti} />}
                label="Spese di pulizia"
              />
              {mostraAnimali && (
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

            {modifica && (modifica.statoPrenotazione === StatoPrenotazione.InCorso || modifica.statoPrenotazione === StatoPrenotazione.Completata) && (
              <>
                <Divider />
                <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>Arrivo effettivo</Typography>
                <TextField
                  type="datetime-local"
                  size="small"
                  label="Ora di arrivo dell'ospite"
                  value={arrivoEffettivo}
                  onChange={(e) => setArrivoEffettivo(e.target.value)}
                  disabled={inCorso}
                  slotProps={{ inputLabel: { shrink: true } }}
                  helperText="Registrata in automatico al check-in. Correggila se l'ospite è arrivato a un orario diverso: da qui decorrono i termini per la schedina alla Polizia di Stato (24 ore, 6 se il soggiorno dura meno di un giorno)."
                />
              </>
            )}

            {puoScrivere &&
              modifica &&
              (modifica.statoPrenotazione === StatoPrenotazione.Incompleta || modifica.statoPrenotazione === StatoPrenotazione.InCorso) && (
                <>
                  <Divider />
                  <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>Servizi durante il soggiorno</Typography>
                  <Typography sx={{ fontSize: 12, color: tokens.textTertiary, mt: -1 }}>
                    Solo se è l&apos;ospite a chiederlo: la regola della struttura resta quella, e la scelta viene registrata nel log. Si
                    salva subito.
                  </Typography>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={rinunce.rinunciaPulizia}
                        onChange={(e) => salvaRinunce({ ...rinunce, rinunciaPulizia: e.target.checked })}
                        disabled={inCorso || aggiornaRinunce.isPending}
                      />
                    }
                    label="L'ospite rinuncia alla pulizia della camera"
                  />
                  <FormControlLabel
                    sx={{ mt: -1.5 }}
                    control={
                      <Checkbox
                        checked={rinunce.rinunciaBiancheria}
                        onChange={(e) => salvaRinunce({ ...rinunce, rinunciaBiancheria: e.target.checked })}
                        disabled={inCorso || aggiornaRinunce.isPending}
                      />
                    }
                    label="L'ospite rinuncia al cambio biancheria"
                  />
                </>
              )}

          </>
        )}
      </DialogContent>

      {/* Fissi in entrambi i tab: aggiungendo un servizio l'importo che cambia resta sotto gli occhi. */}
      <Box sx={{ px: 3, pt: 2, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
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
          {/* In sola lettura: è la somma del registro, si cambia dal tab Pagamenti. */}
          <TextField
            label="Pagato (€)"
            value={pagato.toFixed(2)}
            fullWidth
            onClick={() => setTab('pagamenti')}
            slotProps={{ input: { readOnly: true } }}
            helperText={
              (Number(importoTotale) || 0) - pagato > 0.004
                ? `Da saldare: ${((Number(importoTotale) || 0) - pagato).toFixed(2)} €`
                : (Number(importoTotale) || 0) - pagato < -0.004
                  ? `Pagato in più: ${(pagato - (Number(importoTotale) || 0)).toFixed(2)} €`
                  : ' '
            }
          />
        </Box>
      </Box>

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
        {/* Secondo documento con i soli servizi addebitati dopo la fattura: la proposta salta il resto. */}
        {modifica && puoFatturare && extraDaFatturare && (
          <Button onClick={() => setFatturaExtraAperta(true)} disabled={inCorso}>
            Fattura gli extra
          </Button>
        )}
        {/* Sul trattamento salvato: se è stato appena cambiato, i ticket seguono dopo il salvataggio. */}
        {modifica?.trattamento != null && (trattamenti.data ?? []).some((l) => l.tipo === modifica.trattamento && l.stampaTicket) && (
          <Button onClick={scaricaBuoni} disabled={inCorso}>
            Stampa ticket
          </Button>
        )}
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        {/* Il check-in si può fare solo dal giorno dell'arrivo in poi, mai su una prenotazione futura. */}
        {puoFareCheckInOut &&
          modifica &&
          modifica.statoPrenotazione === StatoPrenotazione.Incompleta &&
          isOggiOPrima(modifica.checkIn) && (
            <Button variant="contained" onClick={eseguiCheckIn} disabled={inCorso}>
              Check-in
            </Button>
          )}
        {puoFareCheckInOut && modifica && modifica.statoPrenotazione === StatoPrenotazione.InCorso && (
          <Button variant="contained" onClick={eseguiCheckOut} disabled={inCorso}>
            Check-out
          </Button>
        )}
        {/* Facoltativo: si può creare anche da qui, i servizi si aggiungono quando servono. */}
        {puoScrivere && !modifica && mostraTabServizi && tabEffettivo === 'soggiorno' && (
          <Button onClick={() => setTab('servizi')} disabled={inCorso}>
            Avanti: servizi
          </Button>
        )}
        {puoScrivere && (
          <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
            {modifica ? 'Salva modifiche' : 'Crea prenotazione'}
          </Button>
        )}
      </DialogActions>

      {checkOutAperto && modifica && (
        <CheckOutDialog
          strutturaId={strutturaId}
          prenotazione={modifica}
          cauzionePrevista={cauzionePrevista && cauzioneAttiva}
          onChiudi={() => setCheckOutAperto(false)}
          onCompletato={onClose}
        />
      )}

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
          messaggio={
            (modifica?.daOta
              ? "Questa prenotazione è arrivata dall'OTA: annullandola qui resta confermata sul portale. Fallo solo se l'OTA l'ha già cancellata e l'annullamento non è arrivato. "
              : '') +
            (pagato > 0
              ? `Annullare questa prenotazione? L'importo totale verrà azzerato. I ${pagato.toFixed(2)} € già pagati restano nel registro: se li restituisci, registra un rimborso nel tab Pagamenti.`
              : "Annullare questa prenotazione? Gli importi verranno azzerati.")
          }
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

      {schedinaBreveDaInviare && modifica && (
        <ConfermaSchedinaSoggiornoBreve
          strutturaId={strutturaId}
          prenotazioneId={modifica.id}
          riferimento={[modifica.ospiteCognome, modifica.ospiteNome].filter(Boolean).join(' ') || modifica.numeroPrenotazione}
          onChiudi={() => {
            setSchedinaBreveDaInviare(false)
            if (fatturaInAttesa) {
              setFatturaDaProporre(fatturaInAttesa)
              setFatturaInAttesa(null)
              return
            }
            onClose()
          }}
        />
      )}

      {fatturaDaProporre && (
        <ProponiFatturaDopoCheckIn strutturaId={strutturaId} prenotazione={fatturaDaProporre} onChiudi={onClose} />
      )}

      {fatturaExtraAperta && modifica && (
        <FatturaDialog
          strutturaId={strutturaId}
          stato={{ modo: 'crea' }}
          prenotazioniDisponibili={[modifica]}
          clienti={[]}
          onClose={() => setFatturaExtraAperta(false)}
        />
      )}
    </Dialog>
  )
}
