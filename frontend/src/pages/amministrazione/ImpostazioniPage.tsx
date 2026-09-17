import { useEffect, useRef, useState, type ReactNode } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useTipologie } from '../../api/tipologie'
import { ApiError } from '../../api/client'
import { useAggiornaImpostazioni, useImpostazioni, type ImpostazioniStrutturaDto, type ImpostazioniStrutturaRequest } from '../../api/impostazioni'
import {
  useAggiornaAlloggiatiWebConfig,
  useAggiornaPayTouristConfig,
  useAggiornaWubookConfig,
  useAlloggiatiWebConfig,
  useEliminaOsservatorioAppartamento,
  useEliminaPayTouristStruttura,
  useOsservatorioAppartamenti,
  usePayTouristConfig,
  usePayTouristStrutture,
  useSuggerimentoEtaTassaPayTourist,
  useVerificaPortaliOnlinePayTourist,
  useWubookConfig,
  type AlloggiatiWebIntegrazioneDto,
  type OsservatorioAppartamentoDto,
  type PayTouristIntegrazioneDto,
  type PayTouristPortaleOnlineDto,
  type PayTouristStrutturaDto,
  type WubookIntegrazioneDto,
} from '../../api/integrazioni'
import { fontDisplay, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { useMobile } from '../../lib/useMobile'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { OsservatorioAppartamentoDialog } from '../../components/OsservatorioAppartamentoDialog'
import { PayTouristStrutturaDialog } from '../../components/PayTouristStrutturaDialog'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

type TabImpostazioni = 'generali' | 'polizia' | 'osservatorio' | 'paytourist'

/** Pallino colorato (verde = inserito, grigio = mancante) — usato come indicatore compatto accanto a un campo o un titolo, al posto di un Chip testuale. */
function Pallino({ inserito }: { inserito: boolean }) {
  return (
    <Box
      component="span"
      sx={{ display: 'inline-block', width: 8, height: 8, borderRadius: '50%', bgcolor: inserito ? tokens.ok600 : tokens.textTertiary, flex: '0 0 auto' }}
    />
  )
}

/** Etichetta di campo con pallino di stato — per i token/credenziali, indica a colpo d'occhio se è già stato inserito. */
function EtichettaConPallino({ testo, inserito }: { testo: string; inserito: boolean }) {
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
      <Pallino inserito={inserito} />
      {testo}
    </Box>
  )
}

export function ImpostazioniPage() {
  const { strutturaId, strutturaCorrente } = useStruttura()
  const [tab, setTab] = useState<TabImpostazioni>('generali')
  // Lettura e scrittura delle credenziali Polizia/Osservatorio/PayTourist condividono lo stesso
  // permesso (StatePoliceSettings) — un utente senza questo permesso non deve vedere nemmeno la
  // tab (fallirebbe comunque con 403 al primo caricamento), non solo il pulsante Salva.
  const puoConfigurareCredenziali = usePuoScrivere('statePoliceSettings')

  // Ogni tab di credenziali/configurazione ha senso solo se il Super Admin ha concesso il relativo
  // servizio a questa struttura — stesso principio già applicato alle voci di menu "Invii automatici"
  // (vedi navItems.ts) e alla sezione "Invii automatici" di questa stessa pagina.
  const mostraPolizia = (strutturaCorrente?.alloggiatiWebAbilitato ?? false) && puoConfigurareCredenziali
  const mostraOsservatorio = (strutturaCorrente?.osservatorioAbilitato ?? false) && puoConfigurareCredenziali
  const mostraPayTourist = (strutturaCorrente?.payTouristAbilitato ?? false) && puoConfigurareCredenziali

  const tabVisibile: Record<TabImpostazioni, boolean> = {
    generali: true,
    polizia: mostraPolizia,
    osservatorio: mostraOsservatorio,
    paytourist: mostraPayTourist,
  }
  const tabEffettivo = tabVisibile[tab] ? tab : 'generali'

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Tabs value={tabEffettivo} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ minHeight: 0 }}>
        <Tab label="Generali" value="generali" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        {mostraPolizia && <Tab label="Alloggiati Web" value="polizia" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />}
        {mostraOsservatorio && <Tab label="Osservatorio Turistico" value="osservatorio" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />}
        {mostraPayTourist && <Tab label="PayTourist" value="paytourist" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />}
      </Tabs>

      {tabEffettivo === 'generali' && <TabGenerali strutturaId={strutturaId} />}
      {tabEffettivo === 'polizia' && <TabAlloggiatiWeb strutturaId={strutturaId} />}
      {tabEffettivo === 'osservatorio' && <TabOsservatorio strutturaId={strutturaId} />}
      {tabEffettivo === 'paytourist' && <TabPayTourist strutturaId={strutturaId} />}
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Generali (toggle invii + tassa di soggiorno + comune attività)
// ---------------------------------------------------------------------------

function TabGenerali({ strutturaId }: { strutturaId: string | null }) {
  const { strutturaCorrente } = useStruttura()
  const impostazioni = useImpostazioni(strutturaId)
  const wubookConfig = useWubookConfig(strutturaId)
  const wubookAbilitato = strutturaCorrente?.wubookAbilitato ?? false

  const wubookBox = !wubookAbilitato ? null : wubookConfig.isLoading ? (
    <Skeleton variant="rounded" height={80} />
  ) : wubookConfig.data ? (
    <WubookAttivoToggle strutturaId={strutturaId!} dati={wubookConfig.data} />
  ) : null

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      {impostazioni.isLoading && <Skeleton variant="rounded" height={380} />}
      {!impostazioni.isLoading && impostazioni.data && strutturaCorrente && (
        <ImpostazioniGeneraliForm strutturaId={strutturaId!} dati={impostazioni.data} servizi={strutturaCorrente} extraColonnaDestra={wubookBox} />
      )}
    </Box>
  )
}

function WubookAttivoToggle({ strutturaId, dati }: { strutturaId: string; dati: WubookIntegrazioneDto }) {
  const puoScrivere = usePuoScrivere('settingRoomWrite')
  const [attivo, setAttivo] = useState(dati.attivo)
  const toast = useToast()
  const aggiorna = useAggiornaWubookConfig(strutturaId)

  function salvaAttivo(checked: boolean) {
    setAttivo(checked)
    aggiorna.mutate({ attivo: checked }, { onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') })
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Servizi · OTA</Typography>
      <FormControlLabel
        control={<Checkbox checked={attivo} onChange={(e) => salvaAttivo(e.target.checked)} disabled={aggiorna.isPending || !puoScrivere} />}
        label="Sincronizzazione OTA attiva per questa struttura"
      />
    </Box>
  )
}

export interface ServiziConcessi {
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
}

export function ImpostazioniGeneraliForm({
  strutturaId,
  dati,
  servizi,
  extraColonnaDestra,
}: {
  strutturaId: string
  dati: ImpostazioniStrutturaDto
  servizi: ServiziConcessi
  extraColonnaDestra?: ReactNode
}) {
  const mobile = useMobile()
  const [poliziaStatoAttiva, setPoliziaStatoAttiva] = useState(dati.poliziaStatoAttiva)
  const [osservatorioAttivo, setOsservatorioAttivo] = useState(dati.osservatorioAttivo)
  const [payTouristAttivo, setPayTouristAttivo] = useState(dati.payTouristAttivo)
  const [oraInvioGiornaliero, setOraInvioGiornaliero] = useState(dati.oraInvioGiornaliero?.slice(0, 5) ?? '04:00')
  const [tassaSoggiornoPrezzo, setTassaSoggiornoPrezzo] = useState(dati.tassaSoggiornoPrezzo != null ? String(dati.tassaSoggiornoPrezzo) : '')
  const [tassaSoggiornoMaxGiorni, setTassaSoggiornoMaxGiorni] = useState(dati.tassaSoggiornoMaxGiorni != null ? String(dati.tassaSoggiornoMaxGiorni) : '')
  const [tassaSoggiornoEtaEsenzioneMinori, setTassaSoggiornoEtaEsenzioneMinori] = useState(
    dati.tassaSoggiornoEtaEsenzioneMinori != null ? String(dati.tassaSoggiornoEtaEsenzioneMinori) : '',
  )
  const [tassaSoggiornoEtaEsenzioneAnziani, setTassaSoggiornoEtaEsenzioneAnziani] = useState(
    dati.tassaSoggiornoEtaEsenzioneAnziani != null ? String(dati.tassaSoggiornoEtaEsenzioneAnziani) : '',
  )
  const [tassaSoggiornoPercentualeResidenti, setTassaSoggiornoPercentualeResidenti] = useState(
    dati.tassaSoggiornoPercentualeResidenti != null ? String(dati.tassaSoggiornoPercentualeResidenti) : '',
  )
  const [tassaSoggiornoPercentualeMinori, setTassaSoggiornoPercentualeMinori] = useState(
    dati.tassaSoggiornoPercentualeMinori != null ? String(dati.tassaSoggiornoPercentualeMinori) : '',
  )
  const [tassaSoggiornoPercentualeAnziani, setTassaSoggiornoPercentualeAnziani] = useState(
    dati.tassaSoggiornoPercentualeAnziani != null ? String(dati.tassaSoggiornoPercentualeAnziani) : '',
  )
  const [comuneAttivita, setComuneAttivita] = useState(dati.comuneAttivita ?? '')
  const toast = useToast()
  const aggiorna = useAggiornaImpostazioni(strutturaId)
  const suggerisciEta = useSuggerimentoEtaTassaPayTourist(strutturaId)

  // Se PayTourist è attivo per la struttura e le soglie/percentuali non sono mai state impostate,
  // si auto-compilano da sole all'apertura della pagina (nessun pulsante da premere) — altrimenti
  // restano libere per l'inserimento manuale. Il ref evita di rilanciare la chiamata ad ogni
  // render (dati/suggerisciEta cambiano riferimento spesso) — un solo tentativo per apertura pagina,
  // mai un secondo silenzioso che sovrascriverebbe una correzione manuale già fatta dall'operatore.
  const tentativoAutoSuggerimento = useRef(false)
  useEffect(() => {
    if (tentativoAutoSuggerimento.current || !servizi.payTouristAbilitato) {
      return
    }
    const maiConfigurato =
      dati.tassaSoggiornoEtaEsenzioneMinori == null && dati.tassaSoggiornoEtaEsenzioneAnziani == null && dati.tassaSoggiornoPercentualeResidenti == null
    if (!maiConfigurato) {
      return
    }
    tentativoAutoSuggerimento.current = true
    suggerisciEta.mutate(undefined, {
      onSuccess: (dato) => {
        if (dato.etaMinori != null) setTassaSoggiornoEtaEsenzioneMinori(String(dato.etaMinori))
        if (dato.etaAnziani != null) setTassaSoggiornoEtaEsenzioneAnziani(String(dato.etaAnziani))
        if (dato.percentualeResidenti != null) setTassaSoggiornoPercentualeResidenti(String(dato.percentualeResidenti))
        if (dato.percentualeMinori != null) setTassaSoggiornoPercentualeMinori(String(dato.percentualeMinori))
        if (dato.percentualeAnziani != null) setTassaSoggiornoPercentualeAnziani(String(dato.percentualeAnziani))
      },
    })
  }, [servizi.payTouristAbilitato, dati, suggerisciEta])

  // Rilancio manuale (a differenza dell'effetto sopra, sempre disponibile anche a campi già
  // valorizzati) — utile se il Comune cambia le regole su PayTourist dopo la prima configurazione.
  function aggiornaDaPayTourist() {
    suggerisciEta.mutate(undefined, {
      onSuccess: (dato) => {
        if (dato.etaMinori != null) setTassaSoggiornoEtaEsenzioneMinori(String(dato.etaMinori))
        if (dato.etaAnziani != null) setTassaSoggiornoEtaEsenzioneAnziani(String(dato.etaAnziani))
        if (dato.percentualeResidenti != null) setTassaSoggiornoPercentualeResidenti(String(dato.percentualeResidenti))
        if (dato.percentualeMinori != null) setTassaSoggiornoPercentualeMinori(String(dato.percentualeMinori))
        if (dato.percentualeAnziani != null) setTassaSoggiornoPercentualeAnziani(String(dato.percentualeAnziani))
      },
    })
  }

  function salva() {
    // PayTourist è in manutenzione ogni giorno dalle 14:00 alle 18:00 — l'orario è condiviso dalle 3
    // integrazioni, quindi se PayTourist è concesso a questa Struttura dal Super Admin (non il
    // checkbox self-service sopra, che si può riaccendere in qualunque momento) non può cadere in
    // quella fascia. Stessa regola verificata anche lato server (fonte di verità).
    if (servizi.payTouristAbilitato && oraInvioGiornaliero >= '14:00' && oraInvioGiornaliero < '18:00') {
      toast.errore("L'orario di invio non può essere tra le 14:00 e le 18:00: PayTourist è in manutenzione in quella fascia oraria.")
      return
    }

    const request: ImpostazioniStrutturaRequest = {
      poliziaStatoAttiva,
      osservatorioAttivo,
      payTouristAttivo,
      oraInvioGiornaliero: oraInvioGiornaliero === '' ? null : `${oraInvioGiornaliero}:00`,
      tassaSoggiornoPrezzo: tassaSoggiornoPrezzo.trim() === '' ? null : Number(tassaSoggiornoPrezzo),
      tassaSoggiornoMaxGiorni: tassaSoggiornoMaxGiorni.trim() === '' ? null : Number(tassaSoggiornoMaxGiorni),
      tassaSoggiornoEtaEsenzioneMinori: tassaSoggiornoEtaEsenzioneMinori.trim() === '' ? null : Number(tassaSoggiornoEtaEsenzioneMinori),
      tassaSoggiornoEtaEsenzioneAnziani: tassaSoggiornoEtaEsenzioneAnziani.trim() === '' ? null : Number(tassaSoggiornoEtaEsenzioneAnziani),
      tassaSoggiornoPercentualeResidenti: tassaSoggiornoPercentualeResidenti.trim() === '' ? null : Number(tassaSoggiornoPercentualeResidenti),
      tassaSoggiornoPercentualeMinori: tassaSoggiornoPercentualeMinori.trim() === '' ? null : Number(tassaSoggiornoPercentualeMinori),
      tassaSoggiornoPercentualeAnziani: tassaSoggiornoPercentualeAnziani.trim() === '' ? null : Number(tassaSoggiornoPercentualeAnziani),
      comuneAttivita: comuneAttivita.trim() === '' ? null : comuneAttivita.trim(),
    }

    aggiorna.mutate(request, {
      onSuccess: () => toast.successo('Impostazioni salvate.'),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const nessunServizioInvii = !servizi.alloggiatiWebAbilitato && !servizi.osservatorioAbilitato && !servizi.payTouristAbilitato

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2.5, alignItems: 'stretch' }}>
        <Box sx={{ flex: '1 1 320px', display: 'flex', flexDirection: 'column', gap: 2.5 }}>
          <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Tassa di soggiorno</Typography>

            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField
                label="Prezzo per persona/notte (€)"
                type="number"
                value={tassaSoggiornoPrezzo}
                onChange={(e) => setTassaSoggiornoPrezzo(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
              />
              <TextField
                label="Numero massimo di notti"
                type="number"
                value={tassaSoggiornoMaxGiorni}
                onChange={(e) => setTassaSoggiornoMaxGiorni(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
                helperText="Oltre questa soglia le notti extra non sono tassate"
              />
            </Box>

            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField
                label="Esenti/scontati minori sotto (anni)"
                type="number"
                value={tassaSoggiornoEtaEsenzioneMinori}
                onChange={(e) => setTassaSoggiornoEtaEsenzioneMinori(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
                helperText="Vuoto = nessuna riduzione automatica per età"
              />
              <TextField
                label="Riduzione minori (%)"
                type="number"
                value={tassaSoggiornoPercentualeMinori}
                onChange={(e) => setTassaSoggiornoPercentualeMinori(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
                helperText="Vuoto = 100% (esenzione piena)"
              />
            </Box>

            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField
                label="Esenti/scontati anziani dai (anni)"
                type="number"
                value={tassaSoggiornoEtaEsenzioneAnziani}
                onChange={(e) => setTassaSoggiornoEtaEsenzioneAnziani(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
                helperText="Vuoto = nessuna riduzione automatica per età"
              />
              <TextField
                label="Riduzione anziani (%)"
                type="number"
                value={tassaSoggiornoPercentualeAnziani}
                onChange={(e) => setTassaSoggiornoPercentualeAnziani(e.target.value)}
                fullWidth
                disabled={aggiorna.isPending}
                helperText="Vuoto = 100% (esenzione piena)"
              />
            </Box>

            {servizi.payTouristAbilitato && (
              <Button size="small" variant="outlined" onClick={aggiornaDaPayTourist} disabled={suggerisciEta.isPending} sx={{ alignSelf: 'flex-start' }}>
                Aggiorna da PayTourist
              </Button>
            )}
            {servizi.payTouristAbilitato && suggerisciEta.isError && (
              <Alert severity="warning" onClose={() => suggerisciEta.reset()}>
                {suggerisciEta.error instanceof ApiError ? suggerisciEta.error.message : 'Impossibile leggere le riduzioni da PayTourist.'}
              </Alert>
            )}
            {servizi.payTouristAbilitato && suggerisciEta.isSuccess && (
              <Alert severity="info" onClose={() => suggerisciEta.reset()}>
                Valori proposti automaticamente in base al comune di {comuneAttivita.toLocaleLowerCase() || 'attività'}: si consiglia di verificarne l'esattezza prima di salvare.
              </Alert>
            )}

            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField
                label="Comune di attività"
                value={comuneAttivita}
                onChange={(e) => setComuneAttivita(e.target.value)}
                disabled={aggiorna.isPending}
                fullWidth
                helperText="Comune dove opera fisicamente la struttura"
              />
              <TextField
                label="Riduzione residenti (%)"
                type="number"
                value={tassaSoggiornoPercentualeResidenti}
                onChange={(e) => setTassaSoggiornoPercentualeResidenti(e.target.value)}
                disabled={aggiorna.isPending}
                fullWidth
                helperText="Vuoto = 100% (esenzione piena)"
              />
            </Box>
          </Box>

          {extraColonnaDestra}
        </Box>

        {!nessunServizioInvii && (
          <Box sx={{ flex: '1 1 320px', border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Invii automatici</Typography>
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Attiva qui i servizi per cui hai già configurato le credenziali nelle rispettive sezioni. L'orario si applica a tutti gli invii
              giornalieri di questa struttura.
            </Typography>

            {servizi.alloggiatiWebAbilitato && (
              <FormControlLabel
                control={<Checkbox checked={poliziaStatoAttiva} onChange={(e) => setPoliziaStatoAttiva(e.target.checked)} disabled={aggiorna.isPending} />}
                label="Invio schedine Polizia di Stato (Alloggiati Web)"
              />
            )}
            {servizi.osservatorioAbilitato && (
              <FormControlLabel
                control={<Checkbox checked={osservatorioAttivo} onChange={(e) => setOsservatorioAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
                label="Invio Osservatorio Turistico"
              />
            )}
            {servizi.payTouristAbilitato && (
              <FormControlLabel
                control={<Checkbox checked={payTouristAttivo} onChange={(e) => setPayTouristAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
                label="Invio PayTourist"
              />
            )}

            <TextField
              label="Orario invio giornaliero"
              type="time"
              value={oraInvioGiornaliero}
              onChange={(e) => setOraInvioGiornaliero(e.target.value)}
              sx={{ width: 260 }}
              slotProps={{ inputLabel: { shrink: true } }}
              disabled={aggiorna.isPending}
              helperText={servizi.payTouristAbilitato ? 'L orario di invio non può essere tra le 14:00 e le 18:00: PayTourist è in manutenzione in quella fascia oraria.' : ' '}
            />
          </Box>
        )}
      </Box>

      <Box>
        <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
          Salva impostazioni
        </Button>
      </Box>
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Alloggiati Web (Polizia di Stato)
// ---------------------------------------------------------------------------

function TabAlloggiatiWeb({ strutturaId }: { strutturaId: string | null }) {
  const config = useAlloggiatiWebConfig(strutturaId)

  return (
    <Box sx={{ maxWidth: 640 }}>
      {config.isLoading && <Skeleton variant="rounded" height={260} />}
      {!config.isLoading && config.data && <AlloggiatiWebCredenzialiForm strutturaId={strutturaId!} dati={config.data} />}
    </Box>
  )
}

function AlloggiatiWebCredenzialiForm({ strutturaId, dati }: { strutturaId: string; dati: AlloggiatiWebIntegrazioneDto }) {
  const mobile = useMobile()
  const [utente, setUtente] = useState(dati.utente ?? '')
  const [password, setPassword] = useState('')
  const [wsKey, setWsKey] = useState('')
  const [esitoConnessione, setEsitoConnessione] = useState<{ ok: boolean; errore: string | null } | null>(null)
  const toast = useToast()

  const aggiorna = useAggiornaAlloggiatiWebConfig(strutturaId)

  function salva() {
    setEsitoConnessione(null)
    aggiorna.mutate(
      { utente: utente.trim() === '' ? null : utente.trim(), password: password.trim() === '' ? null : password.trim(), wsKey: wsKey.trim() === '' ? null : wsKey.trim() },
      {
        onSuccess: (risultato) => {
          setEsitoConnessione({ ok: risultato.connessioneOk, errore: risultato.connessioneErrore })
          setPassword('')
          setWsKey('')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Credenziali Alloggiati Web</Typography>
        <Chip size="small" label={dati.credenzialiConfigurate ? 'Configurato' : 'Non configurato'} sx={{ bgcolor: dati.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
      </Box>

      {esitoConnessione && (
        <Alert severity={esitoConnessione.ok ? 'success' : 'warning'} onClose={() => setEsitoConnessione(null)}>
          {esitoConnessione.ok
            ? 'Credenziali salvate — connessione ad Alloggiati Web verificata con successo.'
            : `Credenziali salvate, ma la verifica della connessione non è riuscita: ${esitoConnessione.errore ?? 'errore sconosciuto'}`}
        </Alert>
      )}

      <TextField label="Utente" value={utente} onChange={(e) => setUtente(e.target.value)} disabled={aggiorna.isPending} />
      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField
          label={<EtichettaConPallino testo="Password" inserito={dati.credenzialiConfigurate} />}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          helperText={dati.credenzialiConfigurate ? "Già salvata: lasciarla vuota non la modifica" : ' '}
        />
        <TextField
          label={<EtichettaConPallino testo="Ws Key" inserito={dati.credenzialiConfigurate} />}
          type="password"
          value={wsKey}
          onChange={(e) => setWsKey(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          helperText={dati.credenzialiConfigurate ? "Già salvata: lasciarla vuota non la modifica" : ' '}
        />
      </Box>

      <Box>
        <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
          Salva credenziali
        </Button>
      </Box>
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Osservatorio Turistico (Appartamenti)
// ---------------------------------------------------------------------------

function TabOsservatorio({ strutturaId }: { strutturaId: string | null }) {
  const mobile = useMobile()
  const appartamenti = useOsservatorioAppartamenti(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const elimina = useEliminaOsservatorioAppartamento(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | OsservatorioAppartamentoDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<OsservatorioAppartamentoDto | null>(null)

  function confermaElimina() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null) })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Una struttura può avere più appartamenti/entità PMS, ognuno con le proprie credenziali e un sottoinsieme di tipologie camera.
      </Typography>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Appartamenti</Typography>
        <BottoneNuovo etichetta="+ Nuovo appartamento" onClick={() => setDialogo('nuovo')} disabilitato={!strutturaId} />
      </Box>

      {(appartamenti.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={220} />}

      {!appartamenti.isLoading && !tipologie.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(appartamenti.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessun appartamento configurato." />}
          {(appartamenti.data ?? []).map((a) => (
            <CardElenco key={a.id}>
              <TestataCardElenco
                titolo={a.nome}
                azioneDestra={
                  <Chip
                    size="small"
                    label={a.credenzialiConfigurate ? 'Configurate' : 'Da configurare'}
                    sx={{ bgcolor: a.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }}
                  />
                }
              />
              <AzioniCardElenco>
                <Tooltip title="Modifica">
                  <IconButton size="small" onClick={() => setDialogo(a)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Elimina">
                  <IconButton size="small" onClick={() => setDaEliminare(a)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </AzioniCardElenco>
            </CardElenco>
          ))}
        </Box>
      )}

      {!appartamenti.isLoading && !tipologie.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell>Credenziali</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(appartamenti.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessun appartamento configurato.
                  </TableCell>
                </TableRow>
              )}
              {(appartamenti.data ?? []).map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{a.nome}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={a.credenzialiConfigurate ? 'Configurate' : 'Da configurare'}
                      sx={{ bgcolor: a.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Modifica">
                      <IconButton size="small" onClick={() => setDialogo(a)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Elimina">
                      <IconButton size="small" onClick={() => setDaEliminare(a)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <OsservatorioAppartamentoDialog
          strutturaId={strutturaId}
          appartamento={dialogo === 'nuovo' ? null : dialogo}
          tipologie={tipologie.data ?? []}
          onClose={() => setDialogo('chiuso')}
        />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare appartamento"
          messaggio={`Eliminare l'appartamento "${daEliminare.nome}"? L'associazione con l'Osservatorio Turistico verrà rimossa.`}
          inCorso={elimina.isPending}
          onConferma={confermaElimina}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}

// ---------------------------------------------------------------------------
// PayTourist (Token + Portale online + Strutture PayTourist)
// ---------------------------------------------------------------------------

function TabPayTourist({ strutturaId }: { strutturaId: string | null }) {
  const mobile = useMobile()
  const config = usePayTouristConfig(strutturaId)
  const strutture = usePayTouristStrutture(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const elimina = useEliminaPayTouristStruttura(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | PayTouristStrutturaDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<PayTouristStrutturaDto | null>(null)

  function confermaElimina() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, { onSuccess: () => setDaEliminare(null) })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      {config.isLoading && <Skeleton variant="rounded" height={160} />}
      {!config.isLoading && config.data && <PayTouristConfigForm strutturaId={strutturaId!} dati={config.data} />}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Strutture PayTourist</Typography>
        <BottoneNuovo etichetta="+ Nuova struttura" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} />
      </Box>

      {(strutture.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={200} />}

      {!strutture.isLoading && !tipologie.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {(strutture.data ?? []).length === 0 && <MessaggioVuotoElenco messaggio="Nessuna struttura PayTourist configurata." />}
          {(strutture.data ?? []).map((s) => (
            <CardElenco key={s.id}>
              <TestataCardElenco titolo={s.nome} />
              <RigaCardMeta voci={[{ etichetta: 'Id PayTourist', valore: s.idStrutturaPaytourist ?? '—' }]} />
              <AzioniCardElenco>
                <Tooltip title="Modifica">
                  <IconButton size="small" onClick={() => setDialogo(s)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Elimina">
                  <IconButton size="small" onClick={() => setDaEliminare(s)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </AzioniCardElenco>
            </CardElenco>
          ))}
        </Box>
      )}

      {!strutture.isLoading && !tipologie.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nome</TableCell>
                <TableCell>Id PayTourist</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(strutture.data ?? []).length === 0 && (
                <TableRow>
                  <TableCell colSpan={3} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    Nessuna struttura PayTourist configurata.
                  </TableCell>
                </TableRow>
              )}
              {(strutture.data ?? []).map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{s.nome}</TableCell>
                  <TableCell>{s.idStrutturaPaytourist ?? '—'}</TableCell>
                  <TableCell align="right">
                    <Tooltip title="Modifica">
                      <IconButton size="small" onClick={() => setDialogo(s)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Elimina">
                      <IconButton size="small" onClick={() => setDaEliminare(s)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <PayTouristStrutturaDialog
          strutturaId={strutturaId}
          strutturaPayTourist={dialogo === 'nuova' ? null : dialogo}
          tipologie={tipologie.data ?? []}
          onClose={() => setDialogo('chiuso')}
        />
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare struttura PayTourist"
          messaggio={`Eliminare la struttura PayTourist "${daEliminare.nome}"? L'associazione verrà rimossa.`}
          inCorso={elimina.isPending}
          onConferma={confermaElimina}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}

function PayTouristConfigForm({ strutturaId, dati }: { strutturaId: string; dati: PayTouristIntegrazioneDto }) {
  const [token, setToken] = useState('')
  const [portaleOnlineAttivo, setPortaleOnlineAttivo] = useState(dati.portaleOnlineAttivo)
  // Portali fra cui scegliere: all'apertura sono quelli già salvati, così la pagina si legge anche
  // senza interrogare PayTourist; diventano l'elenco vero appena lo si ricarica dal portale.
  const [portaliDisponibili, setPortaliDisponibili] = useState<PayTouristPortaleOnlineDto[]>(dati.portaliAttivi)
  const [portaliSelezionati, setPortaliSelezionati] = useState<number[]>(dati.portaliAttivi.map((p) => p.id))
  // Mostrato solo quando è stato salvato un token nuovo: un token rifiutato non arriva mai qui,
  // perché in quel caso il salvataggio fallisce e il messaggio esce come errore.
  const [esitoVerifica, setEsitoVerifica] = useState<{ ok: boolean; errore: string | null } | null>(null)
  const toast = useToast()

  const aggiorna = useAggiornaPayTouristConfig(strutturaId)
  const verificaPortali = useVerificaPortaliOnlinePayTourist(strutturaId)

  /**
   * Spegnere l'opzione non richiede niente; accenderla sì. Ha senso solo se l'ente PayTourist
   * prevede l'incasso tramite portali online: dove non lo prevede, l'opzione attiva non serve a
   * nulla — e prima il motivo si leggeva solo a invio fallito, la sera.
   */
  function cambiaPortaleOnline(attivo: boolean) {
    if (!attivo) {
      setPortaleOnlineAttivo(false)
      return
    }

    caricaPortali(() => setPortaleOnlineAttivo(true))
  }

  /** Chiede a PayTourist quali portali riconosce il Comune. Le spunte già date si conservano solo se quel portale c'è ancora. */
  function caricaPortali(alTermine?: () => void) {
    verificaPortali.mutate(undefined, {
      onSuccess: (esito) => {
        if (!esito.abilitato) {
          toast.errore(esito.messaggio ?? 'Portali online non abilitati su questo ente.')
          return
        }

        const disponibili = esito.portali.map((p) => p.id)
        setPortaliDisponibili(esito.portali)
        setPortaliSelezionati((precedenti) => precedenti.filter((id) => disponibili.includes(id)))
        alTermine?.()
        toast.successo(`Portali riconosciuti dal Comune: ${esito.portali.map((p) => p.nome).join(', ')}.`)
      },
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Verifica non riuscita, riprova.'),
    })
  }

  function cambiaPortaleSelezionato(id: number, scelto: boolean) {
    setPortaliSelezionati((precedenti) => (scelto ? [...precedenti, id] : precedenti.filter((x) => x !== id)))
  }

  function salva() {
    aggiorna.mutate(
      {
        token: token.trim() === '' ? null : token.trim(),
        portaleOnlineAttivo,
        portaliAttivi: portaliDisponibili.filter((p) => portaliSelezionati.includes(p.id)),
      },
      {
        onSuccess: (risultato) => {
          setToken('')
          setEsitoVerifica(risultato.verificaOk === null ? null : { ok: risultato.verificaOk, errore: risultato.verificaErrore })
          toast.successo('Configurazione salvata.')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 640 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Token PayTourist</Typography>
        <Chip size="small" label={dati.tokenConfigurato ? 'Configurato' : 'Non configurato'} sx={{ bgcolor: dati.tokenConfigurato ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
      </Box>

      {esitoVerifica && (
        <Alert severity={esitoVerifica.ok ? 'success' : 'warning'} onClose={() => setEsitoVerifica(null)}>
          {esitoVerifica.ok
            ? 'Token salvato — verificato su PayTourist.'
            : `Token salvato, ma non è stato possibile verificarlo: ${esitoVerifica.errore ?? 'errore sconosciuto'}`}
        </Alert>
      )}

      <TextField
        label={<EtichettaConPallino testo="Token" inserito={dati.tokenConfigurato} />}
        type="password"
        value={token}
        onChange={(e) => setToken(e.target.value)}
        disabled={aggiorna.isPending}
        helperText={dati.tokenConfigurato ? "Già salvato: lasciarlo vuoto non lo modifica" : ' '}
      />

      <FormControlLabel
        control={
          <Checkbox
            checked={portaleOnlineAttivo}
            onChange={(e) => cambiaPortaleOnline(e.target.checked)}
            disabled={aggiorna.isPending || verificaPortali.isPending}
          />
        }
        label="Portale online"
      />

      {portaleOnlineAttivo && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 2, display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>Imposta incassata dal portale</Typography>
          <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
            Spunta i portali che riscuotono loro l'imposta e la versano al Comune. Le prenotazioni degli altri canali vengono
            dichiarate come riscosse da te.
          </Typography>

          {portaliDisponibili.length === 0 ? (
            <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
              Nessun portale caricato: chiedi l'elenco a PayTourist per poter scegliere.
            </Typography>
          ) : (
            portaliDisponibili.map((portale) => (
              <FormControlLabel
                key={portale.id}
                control={
                  <Checkbox
                    checked={portaliSelezionati.includes(portale.id)}
                    onChange={(e) => cambiaPortaleSelezionato(portale.id, e.target.checked)}
                    disabled={aggiorna.isPending || verificaPortali.isPending}
                  />
                }
                label={portale.nome}
              />
            ))
          )}

          <Box>
            <Button size="small" variant="outlined" onClick={() => caricaPortali()} disabled={aggiorna.isPending || verificaPortali.isPending}>
              Aggiorna elenco da PayTourist
            </Button>
          </Box>
        </Box>
      )}

      <Box>
        <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
          Salva
        </Button>
      </Box>
    </Box>
  )
}
