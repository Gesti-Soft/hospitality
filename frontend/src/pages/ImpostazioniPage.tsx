import { useState } from 'react'
import { useQueries } from '@tanstack/react-query'
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
import { useStruttura } from '../struttura/StrutturaContext'
import { useTipologie } from '../api/tipologie'
import { ApiError, apiGet } from '../api/client'
import { useAggiornaImpostazioni, useImpostazioni, type ImpostazioniStrutturaDto, type ImpostazioniStrutturaRequest } from '../api/impostazioni'
import {
  useAggiornaAlloggiatiWebConfig,
  useAggiornaPayTouristConfig,
  useAggiornaWubookConfig,
  useAggiornaWubookLicenza,
  useAlloggiatiWebConfig,
  useOsservatorioAppartamenti,
  usePayTouristConfig,
  usePayTouristStrutture,
  useRinnovaWubookCredenziali,
  useSchedineAlloggiatiWeb,
  useWubookConfig,
  type AlloggiatiWebIntegrazioneDto,
  type OsservatorioAppartamentoDto,
  type PayTouristIntegrazioneDto,
  type PayTouristStrutturaDto,
  type PrenotazionePayTouristDto,
  type SchedinaOsservatorioDto,
  type WubookIntegrazioneDto,
} from '../api/integrazioni'
import { fontDisplay, tokens } from '../theme'
import { OsservatorioAppartamentoDialog } from '../components/OsservatorioAppartamentoDialog'
import { PayTouristStrutturaDialog } from '../components/PayTouristStrutturaDialog'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

type TabImpostazioni = 'generali' | 'licenza' | 'polizia' | 'osservatorio' | 'paytourist'

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
  const { strutturaId } = useStruttura()
  const [tab, setTab] = useState<TabImpostazioni>('generali')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ minHeight: 0 }}>
        <Tab label="Generali" value="generali" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Licenza gestisoft.it" value="licenza" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Alloggiati Web" value="polizia" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="Osservatorio Turistico" value="osservatorio" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        <Tab label="PayTourist" value="paytourist" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
      </Tabs>

      {tab === 'generali' && <TabGenerali strutturaId={strutturaId} />}
      {tab === 'licenza' && <TabLicenzaGestisoft strutturaId={strutturaId} />}
      {tab === 'polizia' && <TabAlloggiatiWeb strutturaId={strutturaId} />}
      {tab === 'osservatorio' && <TabOsservatorio strutturaId={strutturaId} />}
      {tab === 'paytourist' && <TabPayTourist strutturaId={strutturaId} />}
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

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2.5, alignItems: 'flex-start' }}>
      {impostazioni.isLoading && <Skeleton variant="rounded" height={380} sx={{ flex: '1 1 460px', maxWidth: 640 }} />}
      {!impostazioni.isLoading && impostazioni.data && strutturaCorrente && (
        <Box sx={{ flex: '1 1 460px', maxWidth: 640 }}>
          <ImpostazioniGeneraliForm strutturaId={strutturaId!} dati={impostazioni.data} servizi={strutturaCorrente} />
        </Box>
      )}

      {wubookAbilitato && wubookConfig.isLoading && <Skeleton variant="rounded" height={80} sx={{ flex: '1 1 460px', maxWidth: 640 }} />}
      {wubookAbilitato && !wubookConfig.isLoading && wubookConfig.data && (
        <Box sx={{ flex: '1 1 460px', maxWidth: 640 }}>
          <WubookAttivoToggle strutturaId={strutturaId!} dati={wubookConfig.data} />
        </Box>
      )}

      {strutturaId && strutturaCorrente && (
        <Box sx={{ flex: '1 1 100%' }}>
          <StoricoInviiAutomatici strutturaId={strutturaId} servizi={strutturaCorrente} />
        </Box>
      )}
    </Box>
  )
}

type RigaStoricoInvio = { modulo: string; ospite: string; camera: string | null; checkIn: string | null; checkOut: string | null }

/**
 * Storico delle schedine/prenotazioni già inviate ai 3 servizi esterni fino ad oggi — utile perché
 * una prenotazione con Tassa di soggiorno disattivata (vedi PrenotazioneDialog) viene marcata
 * "inviata" senza che nulla venga davvero trasmesso: questa vista rende visibile anche quei casi,
 * non solo gli invii reali. Riusa gli stessi 3 endpoint "elenco schedine" già usati dalle pagine
 * operative (Polizia/Osservatorio/PayTourist, finestra 30 giorni), filtrati qui su "già inviata" —
 * nessuna nuova query backend.
 */
function StoricoInviiAutomatici({ strutturaId, servizi }: { strutturaId: string; servizi: ServiziConcessi }) {
  const schedineAlloggiati = useSchedineAlloggiatiWeb(servizi.alloggiatiWebAbilitato ? strutturaId : null)
  const appartamenti = useOsservatorioAppartamenti(servizi.osservatorioAbilitato ? strutturaId : null)
  const payTouristStrutture = usePayTouristStrutture(servizi.payTouristAbilitato ? strutturaId : null)

  const schedineOsservatorio = useQueries({
    queries: (appartamenti.data ?? []).map((a) => ({
      queryKey: ['osservatorio-schedine', strutturaId, a.id],
      queryFn: () => apiGet<SchedinaOsservatorioDto[]>(`/strutture/${strutturaId}/osservatorio/appartamenti/${a.id}/schedine`),
    })),
  })

  const prenotazioniPayTourist = useQueries({
    queries: (payTouristStrutture.data ?? []).map((s) => ({
      queryKey: ['paytourist-prenotazioni', strutturaId, s.id],
      queryFn: () => apiGet<PrenotazionePayTouristDto[]>(`/strutture/${strutturaId}/paytourist/strutture/${s.id}/prenotazioni`),
    })),
  })

  if (!servizi.alloggiatiWebAbilitato && !servizi.osservatorioAbilitato && !servizi.payTouristAbilitato) {
    return null
  }

  const righe: RigaStoricoInvio[] = [
    ...(schedineAlloggiati.data ?? [])
      .filter((s) => s.inviata)
      .map((s) => ({ modulo: 'Polizia di Stato', ospite: s.nomeOspite, camera: s.camera, checkIn: s.checkIn, checkOut: s.checkOut })),
    ...schedineOsservatorio.flatMap((q) =>
      (q.data ?? [])
        .filter((s) => s.arrivoInviato)
        .map((s) => ({ modulo: 'Osservatorio', ospite: s.nomeOspite, camera: s.camera, checkIn: s.checkIn, checkOut: s.checkOut })),
    ),
    ...prenotazioniPayTourist.flatMap((q) =>
      (q.data ?? [])
        .filter((s) => s.inviata)
        .map((s) => ({ modulo: 'PayTourist', ospite: s.nomeOspite, camera: s.camera, checkIn: s.checkIn, checkOut: s.checkOut })),
    ),
  ].sort((a, b) => new Date(b.checkIn ?? 0).getTime() - new Date(a.checkIn ?? 0).getTime())

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
      <Box sx={{ p: '18px 20px' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Storico invii (ultimi 30 giorni)</Typography>
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary, mt: 0.5 }}>
          Schedine/prenotazioni già inviate ai servizi esterni — include anche quelle marcate come inviate automaticamente (es. tassa di
          soggiorno disattivata per una prenotazione).
        </Typography>
      </Box>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Modulo</TableCell>
            <TableCell>Ospite</TableCell>
            <TableCell>Camera</TableCell>
            <TableCell>Check-in</TableCell>
            <TableCell>Check-out</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {righe.length === 0 && (
            <TableRow>
              <TableCell colSpan={5} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                Nessun invio negli ultimi 30 giorni.
              </TableCell>
            </TableRow>
          )}
          {righe.map((r, i) => (
            <TableRow key={i} hover>
              <TableCell sx={{ fontWeight: 700 }}>{r.modulo}</TableCell>
              <TableCell>{r.ospite}</TableCell>
              <TableCell>{r.camera ?? '—'}</TableCell>
              <TableCell>{r.checkIn ? formattatoreDataOra.format(new Date(r.checkIn)) : '—'}</TableCell>
              <TableCell>{r.checkOut ? formattatoreDataOra.format(new Date(r.checkOut)) : '—'}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  )
}

function WubookAttivoToggle({ strutturaId, dati }: { strutturaId: string; dati: WubookIntegrazioneDto }) {
  const [attivo, setAttivo] = useState(dati.attivo)
  const [errore, setErrore] = useState<string | null>(null)
  const aggiorna = useAggiornaWubookConfig(strutturaId)

  function salvaAttivo(checked: boolean) {
    setAttivo(checked)
    setErrore(null)
    aggiorna.mutate({ attivo: checked }, { onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.') })
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Wubook</Typography>
      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      <FormControlLabel
        control={<Checkbox checked={attivo} onChange={(e) => salvaAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
        label="Sincronizzazione Wubook attiva per questa struttura"
      />
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Richiede la licenza gestisoft.it configurata (tab "Licenza gestisoft.it").
      </Typography>
    </Box>
  )
}

export interface ServiziConcessi {
  alloggiatiWebAbilitato: boolean
  osservatorioAbilitato: boolean
  payTouristAbilitato: boolean
}

export function ImpostazioniGeneraliForm({ strutturaId, dati, servizi }: { strutturaId: string; dati: ImpostazioniStrutturaDto; servizi: ServiziConcessi }) {
  const [poliziaStatoAttiva, setPoliziaStatoAttiva] = useState(dati.poliziaStatoAttiva)
  const [osservatorioAttivo, setOsservatorioAttivo] = useState(dati.osservatorioAttivo)
  const [payTouristAttivo, setPayTouristAttivo] = useState(dati.payTouristAttivo)
  const [oraInvioGiornaliero, setOraInvioGiornaliero] = useState(dati.oraInvioGiornaliero?.slice(0, 5) ?? '04:00')
  const [tassaSoggiornoPrezzo, setTassaSoggiornoPrezzo] = useState(dati.tassaSoggiornoPrezzo != null ? String(dati.tassaSoggiornoPrezzo) : '')
  const [tassaSoggiornoMaxGiorni, setTassaSoggiornoMaxGiorni] = useState(dati.tassaSoggiornoMaxGiorni != null ? String(dati.tassaSoggiornoMaxGiorni) : '')
  const [comuneAttivita, setComuneAttivita] = useState(dati.comuneAttivita ?? '')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiorna = useAggiornaImpostazioni(strutturaId)

  function salva() {
    setErrore(null)
    setSalvato(false)

    const request: ImpostazioniStrutturaRequest = {
      poliziaStatoAttiva,
      osservatorioAttivo,
      payTouristAttivo,
      oraInvioGiornaliero: oraInvioGiornaliero === '' ? null : `${oraInvioGiornaliero}:00`,
      tassaSoggiornoPrezzo: tassaSoggiornoPrezzo.trim() === '' ? null : Number(tassaSoggiornoPrezzo),
      tassaSoggiornoMaxGiorni: tassaSoggiornoMaxGiorni.trim() === '' ? null : Number(tassaSoggiornoMaxGiorni),
      comuneAttivita: comuneAttivita.trim() === '' ? null : comuneAttivita.trim(),
    }

    aggiorna.mutate(request, {
      onSuccess: () => setSalvato(true),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const nessunServizioInvii = !servizi.alloggiatiWebAbilitato && !servizi.osservatorioAbilitato && !servizi.payTouristAbilitato

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      {!nessunServizioInvii && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Invii automatici</Typography>
          <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
            Attiva qui i servizi per cui hai già configurato le credenziali nelle rispettive sezioni. L'orario si applica a tutti gli invii
            giornalieri di questa struttura.
          </Typography>

          {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
          {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Impostazioni salvate.</Alert>}

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
            sx={{ width: 200 }}
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={aggiorna.isPending}
          />
        </Box>
      )}

      {nessunServizioInvii && (errore || salvato) && (
        <Box>
          {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
          {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Impostazioni salvate.</Alert>}
        </Box>
      )}

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Tassa di soggiorno</Typography>

        <Box sx={{ display: 'flex', gap: 2 }}>
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

        <TextField
          label="Comune di attività"
          value={comuneAttivita}
          onChange={(e) => setComuneAttivita(e.target.value)}
          disabled={aggiorna.isPending}
          helperText="Comune dove opera fisicamente la struttura (può differire dalla sede fiscale in Fatturazione) — usato per l'esenzione tassa di soggiorno e le riduzioni PayTourist per residenza"
        />
      </Box>

      <Box>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva impostazioni
        </Button>
      </Box>
    </Box>
  )
}

// ---------------------------------------------------------------------------
// Licenza gestisoft.it (autenticazione condivisa: rinnovo credenziali Wubook + Id Software
// PayTourist — non è una configurazione di Wubook, vedi WubookLicenzaService.RinnovaCredenzialiAsync)
// ---------------------------------------------------------------------------

function TabLicenzaGestisoft({ strutturaId }: { strutturaId: string | null }) {
  const config = useWubookConfig(strutturaId)

  return (
    <Box sx={{ maxWidth: 640 }}>
      {config.isLoading && <Skeleton variant="rounded" height={280} />}
      {!config.isLoading && config.data && <LicenzaGestisoftForm strutturaId={strutturaId!} dati={config.data} />}
    </Box>
  )
}

function LicenzaGestisoftForm({ strutturaId, dati }: { strutturaId: string; dati: WubookIntegrazioneDto }) {
  const [gestisoftUsername, setGestisoftUsername] = useState(dati.gestisoftUsername ?? '')
  const [gestisoftToken, setGestisoftToken] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiornaLicenza = useAggiornaWubookLicenza(strutturaId)
  const rinnova = useRinnovaWubookCredenziali(strutturaId)

  function segnalaErrore(err: unknown) {
    setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function salvaLicenza() {
    setErrore(null)
    setSalvato(false)
    aggiornaLicenza.mutate(
      { gestisoftUsername: gestisoftUsername.trim() === '' ? null : gestisoftUsername.trim(), gestisoftToken: gestisoftToken.trim() === '' ? null : gestisoftToken.trim() },
      {
        onSuccess: () => {
          setSalvato(true)
          setGestisoftToken('')
        },
        onError: segnalaErrore,
      },
    )
  }

  function rinnovaOra() {
    setErrore(null)
    rinnova.mutate(undefined, { onError: segnalaErrore })
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Licenza gestisoft.it</Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Chip size="small" label={dati.licenzaConfigurata ? 'Licenza configurata' : 'Licenza non configurata'} sx={{ bgcolor: dati.licenzaConfigurata ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
          <Chip size="small" label={dati.credenzialiPronte ? 'Credenziali Wubook pronte' : 'In attesa di rinnovo'} sx={{ bgcolor: dati.credenzialiPronte ? tokens.blue600 : tokens.wait600, color: '#fff', fontWeight: 700 }} />
        </Box>
      </Box>

      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Questa NON è una credenziale di Wubook: è l'autenticazione verso gestisoft.it (Utente/Token della licenza), che restituisce sia le
        credenziali Wubook sia l'Id Software PayTourist. Senza questa licenza né la sincronizzazione Wubook né PayTourist possono
        funzionare. Ogni Struttura ha la propria coppia Utente/Token.
      </Typography>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Licenza salvata.</Alert>}
      {dati.ultimoErrore && <Alert severity="warning">{dati.ultimoErrore}</Alert>}

      <Box sx={{ display: 'flex', gap: 2 }}>
        <TextField label="Utente gestisoft.it" value={gestisoftUsername} onChange={(e) => setGestisoftUsername(e.target.value)} fullWidth disabled={aggiornaLicenza.isPending} />
        <TextField
          label={<EtichettaConPallino testo="Token gestisoft.it" inserito={dati.licenzaConfigurata} />}
          type="password"
          value={gestisoftToken}
          onChange={(e) => setGestisoftToken(e.target.value)}
          fullWidth
          disabled={aggiornaLicenza.isPending}
          helperText={dati.licenzaConfigurata ? "Già salvato: lasciarlo vuoto e salvare lo AZZERA" : ' '}
        />
      </Box>

      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Cache aggiornata: {dati.cacheAggiornataAtUtc ? formattatoreDataOra.format(new Date(dati.cacheAggiornataAtUtc)) : 'mai'}
      </Typography>

      <Box sx={{ display: 'flex', gap: 1.5 }}>
        <Button variant="contained" color="secondary" onClick={salvaLicenza} disabled={aggiornaLicenza.isPending}>
          Salva licenza
        </Button>
        <Button variant="outlined" onClick={rinnovaOra} disabled={rinnova.isPending || !dati.licenzaConfigurata}>
          Rinnova credenziali ora
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
  const [utente, setUtente] = useState(dati.utente ?? '')
  const [password, setPassword] = useState('')
  const [wsKey, setWsKey] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiorna = useAggiornaAlloggiatiWebConfig(strutturaId)

  function salva() {
    setErrore(null)
    setSalvato(false)
    aggiorna.mutate(
      { utente: utente.trim() === '' ? null : utente.trim(), password: password.trim() === '' ? null : password.trim(), wsKey: wsKey.trim() === '' ? null : wsKey.trim() },
      {
        onSuccess: () => {
          setSalvato(true)
          setPassword('')
          setWsKey('')
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Credenziali Alloggiati Web</Typography>
        <Chip size="small" label={dati.credenzialiConfigurate ? 'Configurato' : 'Non configurato'} sx={{ bgcolor: dati.credenzialiConfigurate ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
      </Box>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Credenziali salvate.</Alert>}

      <TextField label="Utente" value={utente} onChange={(e) => setUtente(e.target.value)} disabled={aggiorna.isPending} />
      <Box sx={{ display: 'flex', gap: 2 }}>
        <TextField
          label={<EtichettaConPallino testo="Password" inserito={dati.credenzialiConfigurate} />}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          helperText={dati.credenzialiConfigurate ? "Già salvata: lasciarla vuota e salvare la AZZERA" : ' '}
        />
        <TextField
          label={<EtichettaConPallino testo="Ws Key" inserito={dati.credenzialiConfigurate} />}
          type="password"
          value={wsKey}
          onChange={(e) => setWsKey(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
          helperText={dati.credenzialiConfigurate ? "Già salvata: lasciarla vuota e salvare la AZZERA" : ' '}
        />
      </Box>

      <Box>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
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
  const appartamenti = useOsservatorioAppartamenti(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | OsservatorioAppartamentoDto>('chiuso')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Una struttura può avere più appartamenti/entità PMS, ognuno con le proprie credenziali e un sottoinsieme di tipologie camera.
      </Typography>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Appartamenti</Typography>
        <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuovo')} disabled={!strutturaId}>
          + Nuovo appartamento
        </Button>
      </Box>

      {(appartamenti.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={220} />}

      {!appartamenti.isLoading && !tipologie.isLoading && (
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
    </Box>
  )
}

// ---------------------------------------------------------------------------
// PayTourist (Token + Portale online + Strutture PayTourist)
// ---------------------------------------------------------------------------

function TabPayTourist({ strutturaId }: { strutturaId: string | null }) {
  const config = usePayTouristConfig(strutturaId)
  const strutture = usePayTouristStrutture(strutturaId)
  const tipologie = useTipologie(strutturaId)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | PayTouristStrutturaDto>('chiuso')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      {config.isLoading && <Skeleton variant="rounded" height={160} />}
      {!config.isLoading && config.data && <PayTouristConfigForm strutturaId={strutturaId!} dati={config.data} />}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Strutture PayTourist</Typography>
        <Button variant="contained" color="secondary" size="small" onClick={() => setDialogo('nuova')} disabled={!strutturaId}>
          + Nuova struttura
        </Button>
      </Box>

      {(strutture.isLoading || tipologie.isLoading) && <Skeleton variant="rounded" height={200} />}

      {!strutture.isLoading && !tipologie.isLoading && (
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
    </Box>
  )
}

function PayTouristConfigForm({ strutturaId, dati }: { strutturaId: string; dati: PayTouristIntegrazioneDto }) {
  const [token, setToken] = useState('')
  const [portaleOnlineAttivo, setPortaleOnlineAttivo] = useState(dati.portaleOnlineAttivo)
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)

  const aggiorna = useAggiornaPayTouristConfig(strutturaId)

  function salva() {
    setErrore(null)
    setSalvato(false)
    aggiorna.mutate(
      { token: token.trim() === '' ? null : token.trim(), portaleOnlineAttivo },
      {
        onSuccess: () => {
          setSalvato(true)
          setToken('')
        },
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 640 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Token PayTourist</Typography>
        <Chip size="small" label={dati.tokenConfigurato ? 'Configurato' : 'Non configurato'} sx={{ bgcolor: dati.tokenConfigurato ? tokens.ok600 : tokens.textTertiary, color: '#fff', fontWeight: 700 }} />
      </Box>

      {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
      {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Configurazione salvata.</Alert>}

      <TextField
        label={<EtichettaConPallino testo="Token" inserito={dati.tokenConfigurato} />}
        type="password"
        value={token}
        onChange={(e) => setToken(e.target.value)}
        disabled={aggiorna.isPending}
        helperText={dati.tokenConfigurato ? "Già salvato: lasciarlo vuoto e salvare lo AZZERA" : ' '}
      />

      <FormControlLabel
        control={<Checkbox checked={portaleOnlineAttivo} onChange={(e) => setPortaleOnlineAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
        label="Filtra per portale online (scarta le prenotazioni di canali senza un portale PayTourist corrispondente)"
      />

      <Box>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva
        </Button>
      </Box>
    </Box>
  )
}
