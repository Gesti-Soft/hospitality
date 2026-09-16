import { useRef, useState, type ChangeEvent } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/EditOutlined'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdfOutlined'
import CodeIcon from '@mui/icons-material/CodeOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useStoricoPrenotazioni } from '../../api/prenotazioni'
import { ApiError } from '../../api/client'
import {
  AliquotaIva,
  RegimeFiscale,
  scaricaFatturaPdf,
  scaricaFatturaXml,
  useAggiornaDatiAziendali,
  useAnniDisponibiliFatture,
  useCaricaLogoDatiAziendali,
  useDatiAziendali,
  useDatiClienti,
  useFatture,
  useLogoDatiAziendali,
  useRimuoviLogoDatiAziendali,
  type DatiAziendaliDto,
  type DatiAziendaliRequest,
  type DatiClienteDto,
  type DatiFatturaDto,
} from '../../api/fatturazione'
import { fontDisplay, fontMono, stileImporto, tokens } from '../../theme'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useToast } from '../../toast/ToastContext'
import { useMobile } from '../../lib/useMobile'
import { DatiClienteDialog } from '../../components/DatiClienteDialog'
import { ETICHETTA_NATURA, FatturaDialog, type StatoFatturaIniziale } from '../../components/FatturaDialog'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, SentinellaCaricamentoElenco, TestataCardElenco } from '../../components/CardElenco'
import { FiltriRicercaData, nelRangeData, RigaCaricamentoAltri } from '../../components/finanze/FinanzeComuni'
import { usePaginazioneScroll } from '../../lib/usePaginazioneScroll'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { alleggerisciLogo, LOGO_MAX_BYTE, LOGO_TIPI_ACCETTATI } from '../../lib/immagini'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

type Tab_ = 'fatture' | 'dati-aziendali' | 'clienti'

export function FatturazionePage() {
  const { strutturaId } = useStruttura()
  const [tab, setTab] = useState<Tab_>('fatture')
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const toast = useToast()

  const anniDisponibili = useAnniDisponibiliFatture(strutturaId)
  const anni = anniConAnnoCorrente(anniDisponibili.data)
  const fatture = useFatture(strutturaId, anno)
  const clienti = useDatiClienti(strutturaId)
  const storico = useStoricoPrenotazioni(strutturaId, anno)

  function segnalaErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ minHeight: 0 }}>
          <Tab label="Fatture" value="fatture" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Dati aziendali" value="dati-aziendali" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
          <Tab label="Clienti" value="clienti" sx={{ minHeight: 0, fontWeight: 700, fontSize: 13.5 }} />
        </Tabs>

        {tab === 'fatture' && (
          <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
            {anni.map((a) => (
              <MenuItem key={a} value={a}>
                {a}
              </MenuItem>
            ))}
          </TextField>
        )}
      </Box>

      {tab === 'fatture' && (
        <TabFatture
          strutturaId={strutturaId}
          fatture={fatture.data}
          caricamento={fatture.isLoading}
          clienti={clienti.data ?? []}
          prenotazioniDisponibili={storico.data ?? []}
          onErrore={segnalaErrore}
        />
      )}
      {tab === 'dati-aziendali' && <TabDatiAziendali strutturaId={strutturaId} />}
      {tab === 'clienti' && <TabClienti strutturaId={strutturaId} clienti={clienti.data} caricamento={clienti.isLoading} />}
    </Box>
  )
}

function Cornice({ children }: { children: React.ReactNode }) {
  return <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>{children}</Box>
}

function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}

function IntestazioneTab({ titolo, azione }: { titolo: string; azione?: { etichetta: string; onClick: () => void; disabilitato?: boolean } }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>{titolo}</Typography>
      {azione && <BottoneNuovo etichetta={azione.etichetta} onClick={azione.onClick} disabilitato={azione.disabilitato} />}
    </Box>
  )
}

function TabFatture({
  strutturaId,
  fatture,
  caricamento,
  clienti,
  prenotazioniDisponibili,
  onErrore,
}: {
  strutturaId: string | null
  fatture: DatiFatturaDto[] | undefined
  caricamento: boolean
  clienti: DatiClienteDto[]
  prenotazioniDisponibili: ReturnType<typeof useStoricoPrenotazioni>['data']
  onErrore: (err: unknown) => void
}) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('financeWrite')
  const [dialogo, setDialogo] = useState<StatoFatturaIniziale | null>(null)
  const [ricerca, setRicerca] = useState('')
  const [dataDa, setDataDa] = useState('')
  const [dataA, setDataA] = useState('')

  async function scarica(fn: (s: string, id: string, n: number) => Promise<void>, f: DatiFatturaDto) {
    try {
      if (strutturaId) await fn(strutturaId, f.id, f.numeroDocumento)
    } catch (err) {
      onErrore(err)
    }
  }

  const testoRicerca = ricerca.trim().toLowerCase()
  const fattureFiltrate = (fatture ?? [])
    .filter(
      (f) =>
        (!testoRicerca || [String(f.numeroDocumento), f.clienteNome, f.descrizione].some((campo) => campo?.toString().toLowerCase().includes(testoRicerca))) &&
        nelRangeData(f.dataDocumento, dataDa, dataA),
    )
    .sort((a, b) => b.progressivo - a.progressivo)
  // "fatture" (non solo i filtri) tra le dipendenze del reset: cambia riferimento quando l'anno
  // selezionato nella pagina genitore cambia (nuova query), anche se la lunghezza filtrata coincide.
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(fattureFiltrate.length, [testoRicerca, dataDa, dataA, fatture])
  const fattureVisibili = fattureFiltrate.slice(0, righeVisibili)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab
        titolo="Fatture"
        azione={puoScrivere ? { etichetta: '+ Nuova fattura', onClick: () => setDialogo({ modo: 'crea' }), disabilitato: !strutturaId } : undefined}
      />

      <FiltriRicercaData
        ricerca={ricerca}
        onRicercaChange={setRicerca}
        placeholderRicerca="Cerca per numero, cliente o descrizione..."
        dataDa={dataDa}
        onDataDaChange={setDataDa}
        dataA={dataA}
        onDataAChange={setDataA}
      />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {fattureFiltrate.length === 0 && (
            <MessaggioVuotoElenco
              messaggio={testoRicerca || dataDa || dataA ? 'Nessuna fattura corrisponde ai filtri applicati.' : "Nessuna fattura emessa per l'anno selezionato."}
            />
          )}
          {fattureVisibili.map((f) => (
            <CardElenco key={f.id}>
              <TestataCardElenco
                titolo={`${f.numeroDocumento}/${f.anno}`}
                sottotitolo={f.clienteNome ?? undefined}
                azioneDestra={
                  <Box component="span" sx={{ ...stileImporto, fontWeight: 700, fontSize: 15 }}>
                    {formattatoreValuta.format(f.importoTotale)}
                  </Box>
                }
              />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Data', valore: formattatoreData.format(new Date(f.dataDocumento)) },
                  { etichetta: 'Descrizione', valore: f.descrizione ?? '—' },
                ]}
              />
              <AzioniCardElenco>
                {puoScrivere && (
                  <Tooltip title="Modifica">
                    <IconButton size="small" onClick={() => setDialogo({ modo: 'modifica', fattura: f })}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                )}
                <Tooltip title="Scarica PDF">
                  <IconButton size="small" onClick={() => scarica(scaricaFatturaPdf, f)}>
                    <PictureAsPdfIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Scarica XML SDI">
                  <IconButton size="small" onClick={() => scarica(scaricaFatturaXml, f)}>
                    <CodeIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </AzioniCardElenco>
            </CardElenco>
          ))}
          {altreDaCaricare && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!caricamento && !mobile && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>N.</TableCell>
                <TableCell>Data</TableCell>
                <TableCell>Cliente</TableCell>
                <TableCell>Descrizione</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {fattureFiltrate.length === 0 && (
                <RigaVuota
                  colSpan={6}
                  messaggio={testoRicerca || dataDa || dataA ? 'Nessuna fattura corrisponde ai filtri applicati.' : "Nessuna fattura emessa per l'anno selezionato."}
                />
              )}
              {fattureVisibili.map((f) => (
                <TableRow key={f.id} hover>
                  <TableCell sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                    {f.numeroDocumento}/{f.anno}
                  </TableCell>
                  <TableCell sx={{ ...stileImporto }}>{formattatoreData.format(new Date(f.dataDocumento))}</TableCell>
                  <TableCell>{f.clienteNome ?? '—'}</TableCell>
                  <TableCell>{f.descrizione ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ ...stileImporto, fontWeight: 700 }}>
                    {formattatoreValuta.format(f.importoTotale)}
                  </TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <Tooltip title="Modifica">
                        <IconButton size="small" onClick={() => setDialogo({ modo: 'modifica', fattura: f })}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                    <Tooltip title="Scarica PDF">
                      <IconButton size="small" onClick={() => scarica(scaricaFatturaPdf, f)}>
                        <PictureAsPdfIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Scarica XML SDI">
                      <IconButton size="small" onClick={() => scarica(scaricaFatturaXml, f)}>
                        <CodeIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={6} ref={sentinellaRef} />}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo && strutturaId && (
        <FatturaDialog
          strutturaId={strutturaId}
          stato={dialogo}
          prenotazioniDisponibili={prenotazioniDisponibili ?? []}
          clienti={clienti}
          onClose={() => setDialogo(null)}
        />
      )}
    </Box>
  )
}

function TabDatiAziendali({ strutturaId }: { strutturaId: string | null }) {
  const dati = useDatiAziendali(strutturaId)

  if (dati.isLoading) return <Skeleton variant="rounded" height={320} />
  if (!dati.data) return null

  return <DatiAziendaliForm strutturaId={strutturaId!} dati={dati.data} />
}

export function DatiAziendaliForm({ strutturaId, dati }: { strutturaId: string; dati: DatiAziendaliDto }) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('financeWrite')
  const [iso2, setIso2] = useState(dati.iso2 ?? 'IT')
  const [pIva, setPIva] = useState(dati.pIva ?? '')
  const [codiceFiscale, setCodiceFiscale] = useState(dati.codiceFiscale ?? '')
  const [denominazione, setDenominazione] = useState(dati.denominazione ?? '')
  const [nome, setNome] = useState(dati.nome ?? '')
  const [cognome, setCognome] = useState(dati.cognome ?? '')
  const [regimeFiscale, setRegimeFiscale] = useState<string>(dati.regimeFiscale != null ? String(dati.regimeFiscale) : '')
  const [aliquotaIvaDefault, setAliquotaIvaDefault] = useState<string>(dati.aliquotaIvaDefault != null ? String(dati.aliquotaIvaDefault) : '')
  const [naturaDefault, setNaturaDefault] = useState<string>(dati.naturaDefault != null ? String(dati.naturaDefault) : '')
  const [dicituraFattura, setDicituraFattura] = useState(dati.dicituraFattura ?? '')
  const [indirizzo, setIndirizzo] = useState(dati.indirizzo ?? '')
  const [nCivico, setNCivico] = useState(dati.nCivico ?? '')
  const [cap, setCap] = useState(dati.cap ?? '')
  const [comune, setComune] = useState(dati.comune ?? '')
  const [provincia, setProvincia] = useState(dati.provincia ?? '')
  const [nazione, setNazione] = useState(dati.nazione ?? 'Italia')
  const toast = useToast()

  const aggiorna = useAggiornaDatiAziendali(strutturaId)

  function salva() {
    const vuoto = (v: string) => (v.trim() === '' ? null : v.trim())
    const request: DatiAziendaliRequest = {
      iso2: vuoto(iso2),
      pIva: vuoto(pIva),
      codiceFiscale: vuoto(codiceFiscale),
      denominazione: vuoto(denominazione),
      nome: vuoto(nome),
      cognome: vuoto(cognome),
      regimeFiscale: regimeFiscale === '' ? null : (Number(regimeFiscale) as DatiAziendaliDto['regimeFiscale']),
      aliquotaIvaDefault: aliquotaIvaDefault === '' ? null : (Number(aliquotaIvaDefault) as DatiAziendaliDto['aliquotaIvaDefault']),
      naturaDefault: naturaDefault === '' ? null : (Number(naturaDefault) as DatiAziendaliDto['naturaDefault']),
      dicituraFattura: vuoto(dicituraFattura),
      indirizzo: vuoto(indirizzo),
      nCivico: vuoto(nCivico),
      cap: vuoto(cap),
      comune: vuoto(comune),
      provincia: vuoto(provincia),
      nazione: vuoto(nazione),
    }

    aggiorna.mutate(request, {
      onSuccess: () => toast.successo('Dati aziendali salvati.'),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 720 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Profilo fiscale emittente</Typography>
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Questi dati compaiono come mittente su ogni fattura PDF/XML generata per questa struttura.
      </Typography>

      <LogoFattura strutturaId={strutturaId} haLogo={dati.haLogo} />

      <TextField label="Denominazione (se azienda)" value={denominazione} onChange={(e) => setDenominazione(e.target.value)} disabled={aggiorna.isPending} />

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Nome (se ditta individuale)" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={aggiorna.isPending} />
      </Box>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Partita IVA" value={pIva} onChange={(e) => setPIva(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField label="Codice fiscale" value={codiceFiscale} onChange={(e) => setCodiceFiscale(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField select label="Regime fiscale" value={regimeFiscale} onChange={(e) => setRegimeFiscale(e.target.value)} fullWidth disabled={aggiorna.isPending}>
          <MenuItem value="">—</MenuItem>
          <MenuItem value={String(RegimeFiscale.RF01_Ordinario)}>RF01 — Ordinario</MenuItem>
          <MenuItem value={String(RegimeFiscale.RF02_ContribuentiMinimi)}>RF02 — Contribuenti minimi</MenuItem>
          <MenuItem value={String(RegimeFiscale.RF19_Forfettario)}>RF19 — Forfettario</MenuItem>
        </TextField>
      </Box>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Indirizzo" value={indirizzo} onChange={(e) => setIndirizzo(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField label="N. civico" value={nCivico} onChange={(e) => setNCivico(e.target.value)} sx={{ width: 110 }} disabled={aggiorna.isPending} />
        <TextField label="CAP" value={cap} onChange={(e) => setCap(e.target.value)} sx={{ width: 110 }} disabled={aggiorna.isPending} />
      </Box>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Comune" value={comune} onChange={(e) => setComune(e.target.value)} fullWidth disabled={aggiorna.isPending} />
        <TextField label="Provincia" value={provincia} onChange={(e) => setProvincia(e.target.value)} sx={{ width: 110 }} disabled={aggiorna.isPending} />
        <TextField label="Nazione (ISO2)" value={iso2} onChange={(e) => setIso2(e.target.value)} sx={{ width: 170 }} disabled={aggiorna.isPending} />
      </Box>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField label="Nazione (denominazione)" value={nazione} onChange={(e) => setNazione(e.target.value)} fullWidth disabled={aggiorna.isPending} />
      </Box>

      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Aliquota e natura qui sotto sono solo la proposta iniziale di una fattura nuova: restano modificabili su ogni singola fattura.
      </Typography>

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
        <TextField
          select
          label="Aliquota IVA predefinita"
          value={aliquotaIvaDefault}
          onChange={(e) => setAliquotaIvaDefault(e.target.value)}
          fullWidth
          disabled={aggiorna.isPending}
        >
          <MenuItem value="">—</MenuItem>
          {Object.values(AliquotaIva).map((valore) => (
            <MenuItem key={valore} value={String(valore)}>{`${valore}%`}</MenuItem>
          ))}
        </TextField>
        {aliquotaIvaDefault === String(AliquotaIva.Iva0) && (
          <TextField
            select
            label="Natura IVA predefinita"
            value={naturaDefault}
            onChange={(e) => setNaturaDefault(e.target.value)}
            fullWidth
            disabled={aggiorna.isPending}
          >
            <MenuItem value="">—</MenuItem>
            {Object.entries(ETICHETTA_NATURA).map(([valore, etichetta]) => (
              <MenuItem key={valore} value={valore}>
                {etichetta}
              </MenuItem>
            ))}
          </TextField>
        )}
      </Box>

      <TextField
        label="Dicitura in fattura"
        value={dicituraFattura}
        onChange={(e) => setDicituraFattura(e.target.value)}
        multiline
        minRows={2}
        disabled={aggiorna.isPending}
        helperText="La frase che deve comparire in fattura quando l'IVA non si applica — chiedila al commercialista, viene stampata così com'è."
      />

      {puoScrivere && (
        <Box>
          <Button variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
            Salva
          </Button>
        </Box>
      )}
    </Box>
  )
}

/**
 * Logo stampato in testa alla fattura. Sta qui e non nel form dei dati fiscali perche' si salva da
 * solo, appena scelto: un'immagine non ha senso che aspetti il pulsante "Salva" insieme alla
 * partita IVA, e cosi' l'anteprima mostra sempre quello che finira' davvero sul PDF.
 */
function LogoFattura({ strutturaId, haLogo }: { strutturaId: string; haLogo: boolean }) {
  const puoScrivere = usePuoScrivere('financeWrite')
  const logo = useLogoDatiAziendali(strutturaId, haLogo)
  const carica = useCaricaLogoDatiAziendali(strutturaId)
  const rimuovi = useRimuoviLogoDatiAziendali(strutturaId)
  const [confermaRimozione, setConfermaRimozione] = useState(false)
  const inputFile = useRef<HTMLInputElement>(null)
  const toast = useToast()

  const inCorso = carica.isPending || rimuovi.isPending

  async function scegliFile(evento: ChangeEvent<HTMLInputElement>) {
    const file = evento.target.files?.[0]
    // Azzerato subito: senza questo, riscegliere lo stesso file non farebbe scattare l'evento.
    evento.target.value = ''
    if (!file) {
      return
    }

    if (!LOGO_TIPI_ACCETTATI.includes(file.type)) {
      toast.errore("Il logo deve essere un'immagine PNG o JPEG.")
      return
    }

    const leggero = await alleggerisciLogo(file)
    if (leggero.size > LOGO_MAX_BYTE) {
      toast.errore(`Il logo supera ${LOGO_MAX_BYTE / 1024} KB anche dopo il ridimensionamento: usa un'immagine più semplice.`)
      return
    }

    carica.mutate(leggero, {
      onSuccess: () => toast.successo('Logo aggiornato.'),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Caricamento non riuscito, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
      <Box
        sx={{
          width: 160,
          height: 80,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          border: `1px solid ${tokens.surfaceBorder}`,
          borderRadius: 1.5,
          // Sfondo bianco come la carta: e' l'unico modo di vedere in anteprima cosa fara' il PDF.
          bgcolor: '#FFFFFF',
          overflow: 'hidden',
          p: 1,
        }}
      >
        {haLogo && logo.isLoading ? (
          <Skeleton variant="rounded" width={120} height={48} />
        ) : haLogo && logo.data ? (
          <Box component="img" src={logo.data} alt="Logo della struttura" sx={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
        ) : (
          <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>Nessun logo</Typography>
        )}
      </Box>

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700 }}>Logo in fattura</Typography>
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary, maxWidth: 380 }}>
          Compare in alto a sinistra sul PDF, sopra il nome della struttura. PNG o JPEG, massimo{' '}
          {LOGO_MAX_BYTE / 1024} KB: le immagini più grandi vengono rimpicciolite in automatico prima di essere caricate.
        </Typography>

        {puoScrivere && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <input
              ref={inputFile}
              type="file"
              accept={LOGO_TIPI_ACCETTATI.join(',')}
              onChange={(e) => void scegliFile(e)}
              style={{ display: 'none' }}
            />
            <Button size="small" variant="outlined" color="secondary" onClick={() => inputFile.current?.click()} disabled={inCorso}>
              {haLogo ? 'Sostituisci' : 'Carica logo'}
            </Button>
            {haLogo && (
              <Button size="small" color="error" onClick={() => setConfermaRimozione(true)} disabled={inCorso}>
                Rimuovi
              </Button>
            )}
          </Box>
        )}
      </Box>

      {confermaRimozione && (
        <ConfirmDialog
          titolo="Rimuovere il logo?"
          messaggio="Le fatture generate da qui in avanti torneranno senza logo. I documenti già scaricati non cambiano."
          testoConferma="Rimuovi"
          inCorso={rimuovi.isPending}
          onAnnulla={() => setConfermaRimozione(false)}
          onConferma={() =>
            rimuovi.mutate(undefined, {
              onSuccess: () => {
                setConfermaRimozione(false)
                toast.successo('Logo rimosso.')
              },
              onError: (err) => {
                setConfermaRimozione(false)
                toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
              },
            })
          }
        />
      )}
    </Box>
  )
}

function TabClienti({ strutturaId, clienti, caricamento }: { strutturaId: string | null; clienti: DatiClienteDto[] | undefined; caricamento: boolean }) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('financeWrite')
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuovo' | DatiClienteDto>('chiuso')
  const [ricerca, setRicerca] = useState('')

  const testoRicerca = ricerca.trim().toLowerCase()
  const clientiFiltrati = (clienti ?? []).filter(
    (c) =>
      !testoRicerca ||
      [c.denominazione, c.nome, c.cognome, c.pIva, c.codiceFiscale, c.luogoResidenza, c.pec].some((campo) => campo?.toLowerCase().includes(testoRicerca)),
  )
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(clientiFiltrati.length, [testoRicerca, clienti])
  const clientiVisibili = clientiFiltrati.slice(0, righeVisibili)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <IntestazioneTab
        titolo="Clienti fatturabili"
        azione={puoScrivere ? { etichetta: '+ Nuovo cliente', onClick: () => setDialogo('nuovo'), disabilitato: !strutturaId } : undefined}
      />
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary, mt: -1.5 }}>
        Creati automaticamente alla prima fattura per un ospite (deduplicati); qui puoi anche aggiungerne o correggerne uno a mano.
      </Typography>

      {/* Nessun filtro per data qui: un Cliente fatturabile è un'anagrafica, non ha una data propria — quella è sulle sue fatture, in "Fatture". */}
      <FiltriRicercaData ricerca={ricerca} onRicercaChange={setRicerca} placeholderRicerca="Cerca per nominativo, P.IVA/CF, residenza o PEC..." />

      {caricamento && <Skeleton variant="rounded" height={220} />}

      {!caricamento && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {clientiFiltrati.length === 0 && (
            <MessaggioVuotoElenco messaggio={testoRicerca ? 'Nessun cliente corrisponde alla ricerca.' : 'Nessun cliente registrato.'} />
          )}
          {clientiVisibili.map((c) => (
            <CardElenco key={c.id}>
              <TestataCardElenco
                titolo={c.denominazione || `${c.nome ?? ''} ${c.cognome ?? ''}`.trim() || '—'}
                sottotitolo={c.pIva || c.codiceFiscale || undefined}
              />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Residenza', valore: c.luogoResidenza ?? '—' },
                  { etichetta: 'PEC', valore: c.pec ?? '—' },
                ]}
              />
              {puoScrivere && (
                <AzioniCardElenco>
                  <IconButton size="small" onClick={() => setDialogo(c)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
          {altreDaCaricare && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!caricamento && !mobile && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nominativo</TableCell>
                <TableCell>P.IVA / Cod. fiscale</TableCell>
                <TableCell>Residenza</TableCell>
                <TableCell>PEC</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {clientiFiltrati.length === 0 && (
                <RigaVuota colSpan={5} messaggio={testoRicerca ? 'Nessun cliente corrisponde alla ricerca.' : 'Nessun cliente registrato.'} />
              )}
              {clientiVisibili.map((c) => (
                <TableRow key={c.id} hover>
                  <TableCell sx={{ fontWeight: 700 }}>{c.denominazione || `${c.nome ?? ''} ${c.cognome ?? ''}`.trim() || '—'}</TableCell>
                  <TableCell>{c.pIva || c.codiceFiscale || '—'}</TableCell>
                  <TableCell>{c.luogoResidenza ?? '—'}</TableCell>
                  <TableCell>{c.pec ?? '—'}</TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <IconButton size="small" onClick={() => setDialogo(c)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={5} ref={sentinellaRef} />}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && strutturaId && (
        <DatiClienteDialog strutturaId={strutturaId} cliente={dialogo === 'nuovo' ? null : dialogo} onClose={() => setDialogo('chiuso')} />
      )}
    </Box>
  )
}
