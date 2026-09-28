import { useEffect, useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/CloseOutlined'
import { ApiError } from '../api/client'
import type { PrenotazioneDto } from '../api/prenotazioni'
import {
  AliquotaIva,
  NaturaIva,
  RegimeFiscale,
  TipoDocumentoFattura,
  TipoEmissioneDocumento,
  TipoRigaFattura,
  ETICHETTA_MODALITA_PAGAMENTO,
  type ModalitaPagamento,
  useAggiornaFattura,
  useCreaFattura,
  useDatiAziendali,
  usePropostaFattura,
  useRisolviClientePerPrenotazione,
  type AggiornaFatturaRequest,
  type CreaFatturaDaPrenotazioneRequest,
  type DatiClienteDto,
  type DatiFatturaDto,
  type RigaFatturaDto,
  type RigaFatturaRichiesta,
} from '../api/fatturazione'
import { useMobile } from '../lib/useMobile'
import { tokens } from '../theme'
import { DatiClienteDialog } from './DatiClienteDialog'

export type StatoFatturaIniziale = { modo: 'crea' } | { modo: 'modifica'; fattura: DatiFatturaDto }

const ETICHETTA_TIPO_DOCUMENTO: Record<TipoDocumentoFattura, string> = {
  [TipoDocumentoFattura.TD01_Fattura]: 'TD01 — Fattura',
  [TipoDocumentoFattura.TD04_NotaDiCredito]: 'TD04 — Nota di credito',
  [TipoDocumentoFattura.TD06_Parcella]: 'TD06 — Parcella',
}

const ETICHETTA_REGIME: Record<RegimeFiscale, string> = {
  [RegimeFiscale.RF01_Ordinario]: 'RF01 — Ordinario',
  [RegimeFiscale.RF02_ContribuentiMinimi]: 'RF02 — Contribuenti minimi',
  [RegimeFiscale.RF19_Forfettario]: 'RF19 — Forfettario',
}

export const ETICHETTA_NATURA: Record<NaturaIva, string> = {
  [NaturaIva.N1_EscluseArt15]: 'N1 — Escluse art. 15',
  [NaturaIva.N2_2_NonSoggetteAltriCasi]: 'N2.2 — Non soggette',
  [NaturaIva.N4_Esenti]: 'N4 — Esenti',
}

/**
 * Scelta dell'IVA di una riga in un solo campo: un'aliquota ("a10") o, con l'IVA a 0%, la natura
 * ("n22"), che dice perché l'IVA non c'è. È la stessa regola del backend e del tracciato SdI.
 */
const ALIQUOTE_POSITIVE = (Object.values(AliquotaIva) as AliquotaIva[]).filter((a) => a > 0)
const NATURE = Object.keys(ETICHETTA_NATURA).map(Number) as NaturaIva[]

/** Regimi senza IVA in fattura (art. 1, commi 54-89, L. 190/2014 e minimi): natura N2.2 su ogni riga. */
const REGIMI_SENZA_IVA: RegimeFiscale[] = [RegimeFiscale.RF19_Forfettario, RegimeFiscale.RF02_ContribuentiMinimi]
const IVA_SENZA_IVA = `n${NaturaIva.N2_2_NonSoggetteAltriCasi}`

function codiceIva(aliquota: AliquotaIva | null, natura: NaturaIva | null): string {
  if (aliquota != null && aliquota > 0) return `a${aliquota}`
  if (natura != null) return `n${natura}`
  return ''
}

function daCodiceIva(codice: string): { aliquotaIva: AliquotaIva | null; natura: NaturaIva | null } {
  if (codice.startsWith('a')) return { aliquotaIva: Number(codice.slice(1)) as AliquotaIva, natura: null }
  if (codice.startsWith('n')) return { aliquotaIva: AliquotaIva.Iva0, natura: Number(codice.slice(1)) as NaturaIva }
  return { aliquotaIva: null, natura: null }
}

/** Riga nel form: numeri come testo finché si scrivono. */
interface RigaForm {
  chiave: string
  descrizione: string
  quantita: string
  prezzoUnitario: string
  iva: string
  tipo: TipoRigaFattura
  prenotazioneId: string | null
  prenotazioneServizioId: string | null
}

function daRiga(r: RigaFatturaDto, chiave: string): RigaForm {
  return {
    chiave,
    descrizione: r.descrizione,
    quantita: String(r.quantita),
    prezzoUnitario: String(r.prezzoUnitario),
    iva: codiceIva(r.aliquotaIva, r.natura),
    tipo: r.tipo,
    prenotazioneId: r.prenotazioneId,
    prenotazioneServizioId: r.prenotazioneServizioId,
  }
}

const formattatoreEuro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const centesimi = (valore: number) => Math.round(valore * 100)

interface Props {
  strutturaId: string
  stato: StatoFatturaIniziale
  prenotazioniDisponibili: PrenotazioneDto[]
  clienti: DatiClienteDto[]
  onClose: () => void
}

/** Etichetta mostrata nella select ricercabile: numero, ospite (se la scheda alloggiati è compilata), camera e check-in. */
function etichettaPrenotazione(p: PrenotazioneDto): string {
  const numero = p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : p.id.slice(0, 8)
  const ospite = p.ospiteNome || p.ospiteCognome ? `${p.ospiteNome ?? ''} ${p.ospiteCognome ?? ''}`.trim() : null
  const data = p.checkIn ? new Date(p.checkIn).toLocaleDateString('it-IT') : '—'
  return [numero, ospite, p.cameraNome ?? '—', data].filter(Boolean).join(' — ')
}

/**
 * Fattura o ricevuta a righe: il soggiorno e ogni servizio extra su una riga, ognuna con la sua IVA
 * (l'alloggio al 10%, la SPA magari al 22%). Può fatturare più prenotazioni insieme quando paga una
 * sola persona: la prima scelta è di chi paga ed è l'intestatario. Le righe le propone il backend
 * (solo quello che non è già fatturato altrove) e restano modificabili prima di emettere.
 */
export function FatturaDialog({ strutturaId, stato, prenotazioniDisponibili, clienti, onClose }: Props) {
  const mobile = useMobile()
  const modifica = stato.modo === 'modifica' ? stato.fattura : null

  // Se c'è una sola prenotazione disponibile (es. aperto da "Genera fattura" sulla scheda ospiti di
  // una prenotazione specifica), è già preselezionata come quella di chi paga.
  const [prenotazioneId, setPrenotazioneId] = useState(prenotazioniDisponibili.length === 1 ? prenotazioniDisponibili[0].id : '')
  const [altrePrenotazioni, setAltrePrenotazioni] = useState<string[]>([])
  const prenotazioneIds = prenotazioneId === '' ? [] : [prenotazioneId, ...altrePrenotazioni.filter((id) => id !== prenotazioneId)]
  const [datiClienteId, setDatiClienteId] = useState(modifica?.datiClienteId ?? '')
  // Fattura o ricevuta: chi ha partita IVA emette fattura, un privato che affitta emette ricevuta.
  // Non si cambia in modifica — sono due numerazioni diverse, e il numero è già stato assegnato.
  const [tipoEmissione, setTipoEmissione] = useState<TipoEmissioneDocumento>(modifica?.tipoEmissione ?? TipoEmissioneDocumento.Fattura)
  const ricevuta = tipoEmissione === TipoEmissioneDocumento.Ricevuta
  const [tipoDocumento, setTipoDocumento] = useState<string>(modifica?.tipoDocumento != null ? String(modifica.tipoDocumento) : String(TipoDocumentoFattura.TD01_Fattura))
  const [regimeFiscale, setRegimeFiscale] = useState<string>(modifica?.regimeFiscale != null ? String(modifica.regimeFiscale) : String(RegimeFiscale.RF01_Ordinario))
  const [divisa, setDivisa] = useState(modifica?.divisa ?? 'EUR')
  const [modalitaPagamento, setModalitaPagamento] = useState<string>(modifica?.modalitaPagamento != null ? String(modifica.modalitaPagamento) : '')
  const [errore, setErrore] = useState<string | null>(null)
  // Cliente fatturabile appena creato (bare-bones) per la prenotazione di chi paga, da completare con
  // P.IVA/CF/indirizzo/PEC prima di procedere — vedi effect sotto.
  const [clienteDaCompletare, setClienteDaCompletare] = useState<DatiClienteDto | null>(null)

  const crea = useCreaFattura(strutturaId)
  const aggiorna = useAggiornaFattura(strutturaId)
  const risolviCliente = useRisolviClientePerPrenotazione(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  // Le righe: in modifica quelle della fattura; in creazione quelle proposte dal backend, finché
  // l'operatore non ne tocca una. Cambiare prenotazioni o tipo di documento riparte dalla proposta.
  const proposta = usePropostaFattura(strutturaId, prenotazioneIds, tipoEmissione, !modifica)
  const [righeModificate, setRigheModificate] = useState<RigaForm[] | null>(null)
  const prossimaChiave = useRef(1)
  const righeScritte: RigaForm[] =
    righeModificate ??
    (modifica
      ? modifica.righe.map((r) => daRiga(r, `r${r.numero}`))
      : (proposta.data?.righe ?? []).map((r, i) => daRiga(r, `p${i}`)))
  // Forfettari e minimi non applicano l'IVA: ogni riga è 0% N2.2, senza doverlo scegliere riga per riga.
  const senzaIva = !ricevuta && REGIMI_SENZA_IVA.includes(Number(regimeFiscale) as RegimeFiscale)
  const righe = senzaIva ? righeScritte.map((r) => ({ ...r, iva: IVA_SENZA_IVA })) : righeScritte
  const [impostaSoggiornoModificata, setImpostaSoggiornoModificata] = useState<string | null>(null)
  const impostaSoggiorno =
    impostaSoggiornoModificata ??
    (modifica
      ? modifica.impostaSoggiorno != null
        ? String(modifica.impostaSoggiorno)
        : ''
      : proposta.data && proposta.data.impostaSoggiorno > 0
        ? String(proposta.data.impostaSoggiorno)
        : '')

  function ripartiDallaProposta() {
    setRigheModificate(null)
    setImpostaSoggiornoModificata(null)
  }

  function aggiornaRiga(chiave: string, modifiche: Partial<RigaForm>) {
    setRigheModificate(righe.map((r) => (r.chiave === chiave ? { ...r, ...modifiche } : r)))
  }

  // Regime fiscale e aliquota di una fattura nuova arrivano dal profilo fiscale della struttura:
  // dipendono da chi emette, non dalla singola fattura. Si applicano una volta sola, quando i dati
  // arrivano, e mai in modifica — lì contano i valori con cui è stata emessa.
  const datiAziendali = useDatiAziendali(strutturaId)
  const predefinitiApplicati = useRef(false)
  useEffect(() => {
    if (modifica !== null || predefinitiApplicati.current || !datiAziendali.data) return
    predefinitiApplicati.current = true
    if (datiAziendali.data.regimeFiscale != null) setRegimeFiscale(String(datiAziendali.data.regimeFiscale))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [datiAziendali.data])
  const ivaPredefinita = datiAziendali.data
    ? codiceIva(datiAziendali.data.aliquotaIvaDefault, datiAziendali.data.aliquotaIvaDefault === AliquotaIva.Iva0 ? datiAziendali.data.naturaDefault : null)
    : `a${AliquotaIva.Iva10}`

  // Ogni volta che si sceglie la prenotazione di chi paga (mai in modifica, dove il Cliente è già
  // assegnato), chiede un'ANTEPRIMA (nessuna scrittura) del Cliente fatturabile collegato al suo ospite.
  // Se non esiste ancora, il form "Nuovo cliente" si apre precompilato ma NON crea nulla finché
  // l'operatore non preme "Crea cliente" lì dentro — annullare non lascia un Cliente orfano.
  useEffect(() => {
    if (modifica || prenotazioneId === '') return
    risolviCliente.mutate(prenotazioneId, {
      onSuccess: (risultato) => {
        if (risultato.appenaCreato) {
          setClienteDaCompletare(risultato.cliente)
        }
      },
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prenotazioneId])

  // Anteprima dei totali, in centesimi: l'IVA sul totale di ogni aliquota, come il backend e lo SdI.
  const totaliRiga = righe.map((r) => centesimi((Number(r.quantita) || 0) * (Number(r.prezzoUnitario) || 0)))
  const imponibile = totaliRiga.reduce((a, b) => a + b, 0)
  const perAliquota = new Map<number, number>()
  righe.forEach((r, i) => {
    const { aliquotaIva } = daCodiceIva(r.iva)
    if (!ricevuta && aliquotaIva != null && aliquotaIva > 0) perAliquota.set(aliquotaIva, (perAliquota.get(aliquotaIva) ?? 0) + totaliRiga[i])
  })
  const ivaPerAliquota = [...perAliquota.entries()].sort((a, b) => b[0] - a[0]).map(([aliquota, base]) => ({ aliquota, base, iva: Math.round((base * aliquota) / 100) }))
  const iva = ivaPerAliquota.reduce((a, r) => a + r.iva, 0)
  const tassa = centesimi(Number(impostaSoggiorno) || 0)
  const totale = imponibile + iva + tassa

  const prenotazioniScelte = prenotazioneIds.map((id) => prenotazioniDisponibili.find((p) => p.id === id)).filter((p): p is PrenotazioneDto => !!p)

  function salva() {
    if (!modifica && prenotazioneId === '') {
      setErrore('Seleziona la prenotazione di chi paga.')
      return
    }
    if (righe.length === 0) {
      setErrore('Il documento deve avere almeno una riga.')
      return
    }
    const indiceSbagliato = righe.findIndex(
      (r) => r.descrizione.trim() === '' || !(Number(r.quantita) > 0) || r.prezzoUnitario.trim() === '' || (!ricevuta && r.iva === ''),
    )
    if (indiceSbagliato >= 0) {
      setErrore(`Riga ${indiceSbagliato + 1}: servono descrizione, quantità maggiore di zero, prezzo${ricevuta ? '' : ' e IVA'}.`)
      return
    }
    setErrore(null)

    const righeRichiesta: RigaFatturaRichiesta[] = righe.map((r) => ({
      descrizione: r.descrizione.trim(),
      quantita: Number(r.quantita),
      prezzoUnitario: Number(r.prezzoUnitario),
      ...(ricevuta ? { aliquotaIva: null, natura: null } : daCodiceIva(r.iva)),
      tipo: r.tipo,
      prenotazioneId: r.prenotazioneId,
      prenotazioneServizioId: r.prenotazioneServizioId,
    }))
    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (modifica) {
      const request: AggiornaFatturaRequest = {
        datiClienteId: datiClienteId === '' ? null : datiClienteId,
        tipoDocumento: Number(tipoDocumento) as TipoDocumentoFattura,
        regimeFiscale: Number(regimeFiscale) as RegimeFiscale,
        divisa: divisa.trim() === '' ? null : divisa.trim(),
        righe: righeRichiesta,
        impostaSoggiorno: impostaSoggiorno.trim() === '' ? null : Number(impostaSoggiorno),
        modalitaPagamento: ricevuta && modalitaPagamento !== '' ? (Number(modalitaPagamento) as ModalitaPagamento) : null,
      }
      aggiorna.mutate({ fatturaId: modifica.id, request }, { onSuccess: onClose, onError })
    } else {
      const request: CreaFatturaDaPrenotazioneRequest = {
        prenotazioneIds,
        tipoDocumento: Number(tipoDocumento) as TipoDocumentoFattura,
        regimeFiscale: Number(regimeFiscale) as RegimeFiscale,
        divisa: divisa.trim() === '' ? null : divisa.trim(),
        righe: righeRichiesta,
        impostaSoggiorno: impostaSoggiorno.trim() === '' ? null : Number(impostaSoggiorno),
        modalitaPagamento: ricevuta && modalitaPagamento !== '' ? (Number(modalitaPagamento) as ModalitaPagamento) : null,
        tipoEmissione,
      }
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <>
      <Dialog open onClose={onClose} maxWidth="md" fullWidth fullScreen={mobile}>
        <DialogTitle>{modifica ? `Modifica ${ricevuta ? 'ricevuta' : 'fattura'} n. ${modifica.numeroDocumento}` : 'Nuova fattura da prenotazione'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
          <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

          {!modifica && (
            <Autocomplete
              options={prenotazioniDisponibili}
              getOptionLabel={etichettaPrenotazione}
              isOptionEqualToValue={(a, b) => a.id === b.id}
              value={prenotazioniDisponibili.find((p) => p.id === prenotazioneId) ?? null}
              onChange={(_, v) => {
                setPrenotazioneId(v?.id ?? '')
                ripartiDallaProposta()
              }}
              disabled={inCorso}
              noOptionsText="Nessuna prenotazione disponibile"
              renderInput={(params) => (
                <TextField {...params} label="Prenotazione di chi paga" required placeholder="Cerca per numero o ospite..." helperText="Il documento è intestato a questo ospite." />
              )}
            />
          )}

          {/* Due famiglie, paga una: le altre prenotazioni finiscono sullo stesso documento. */}
          {!modifica && prenotazioneId !== '' && prenotazioniDisponibili.length > 1 && (
            <Autocomplete
              multiple
              options={prenotazioniDisponibili.filter((p) => p.id !== prenotazioneId)}
              getOptionLabel={etichettaPrenotazione}
              isOptionEqualToValue={(a, b) => a.id === b.id}
              value={prenotazioniDisponibili.filter((p) => altrePrenotazioni.includes(p.id) && p.id !== prenotazioneId)}
              onChange={(_, v) => {
                setAltrePrenotazioni(v.map((p) => p.id))
                ripartiDallaProposta()
              }}
              disabled={inCorso}
              noOptionsText="Nessun'altra prenotazione"
              renderInput={(params) => <TextField {...params} label="Altre prenotazioni pagate dallo stesso cliente (facoltativo)" placeholder="Cerca..." />}
            />
          )}

          {!modifica && prenotazioneId !== '' && risolviCliente.isPending && (
            <Box sx={{ fontSize: 12, color: 'text.secondary' }}>Verifico il cliente fatturabile collegato a questo ospite...</Box>
          )}
          {!modifica && risolviCliente.data && risolviCliente.variables === prenotazioneId && (
            <Box sx={{ fontSize: 12, color: 'text.secondary' }}>
              Cliente:{' '}
              <b>
                {risolviCliente.data.cliente.denominazione ||
                  `${risolviCliente.data.cliente.nome ?? ''} ${risolviCliente.data.cliente.cognome ?? ''}`.trim()}
              </b>
            </Box>
          )}

          {modifica && (
            <TextField select label="Cliente assegnato" value={datiClienteId ?? ''} onChange={(e) => setDatiClienteId(e.target.value)} disabled={inCorso}>
              <MenuItem value="">Nessuno</MenuItem>
              {clienti.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.denominazione || `${c.nome ?? ''} ${c.cognome ?? ''}`.trim()}
                </MenuItem>
              ))}
            </TextField>
          )}

          {!modifica && (
            <TextField
              select
              label="Documento da emettere"
              value={String(tipoEmissione)}
              onChange={(e) => {
                setTipoEmissione(Number(e.target.value) as TipoEmissioneDocumento)
                ripartiDallaProposta()
              }}
              disabled={inCorso}
              helperText={
                ricevuta
                  ? 'Locazione breve di un privato: fuori dal campo IVA, nessun file per lo SdI, bollo di 2 € sopra 77,47 €.'
                  : 'Chi ha partita IVA, anche in regime forfettario: si genera anche il file per lo SdI.'
              }
            >
              <MenuItem value={String(TipoEmissioneDocumento.Fattura)}>Fattura</MenuItem>
              <MenuItem value={String(TipoEmissioneDocumento.Ricevuta)}>Ricevuta (locazione breve)</MenuItem>
            </TextField>
          )}

          {/* Avviso, non blocco: la scelta fiscale resta del cliente. */}
          {!modifica && ricevuta && prenotazioniScelte.some((p) => p.trattamento != null) && (
            <Alert severity="warning">
              Una prenotazione comprende un trattamento (colazione o pasti): con servizi di ristorazione la locazione breve e la cedolare secca
              potrebbero non essere applicabili (art. 4 DL 50/2017). Verifica con il commercialista prima di emettere la ricevuta.
            </Alert>
          )}

          {!modifica && (proposta.data?.avvisi.length ?? 0) > 0 && (
            <Alert severity="info">
              {proposta.data!.avvisi.map((a) => (
                <Box key={a}>{a}</Box>
              ))}
            </Alert>
          )}
          {!modifica && proposta.error && (
            <Alert severity="error">{proposta.error instanceof ApiError ? proposta.error.message : 'Proposta non disponibile, riprova.'}</Alert>
          )}

          {ricevuta && (
            <TextField
              select
              label="Pagato con"
              value={modalitaPagamento}
              onChange={(e) => setModalitaPagamento(e.target.value)}
              disabled={inCorso}
              helperText="Viene stampato sulla ricevuta. Per un pagamento in contanti la ricevuta è obbligatoria."
            >
              <MenuItem value="">Non indicato</MenuItem>
              {Object.entries(ETICHETTA_MODALITA_PAGAMENTO).map(([valore, etichetta]) => (
                <MenuItem key={valore} value={valore}>
                  {etichetta}
                </MenuItem>
              ))}
            </TextField>
          )}

          <Box sx={{ display: ricevuta ? 'none' : 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField select label="Tipo documento" value={tipoDocumento} onChange={(e) => setTipoDocumento(e.target.value)} fullWidth disabled={inCorso}>
              {Object.entries(ETICHETTA_TIPO_DOCUMENTO).map(([valore, etichetta]) => (
                <MenuItem key={valore} value={valore}>
                  {etichetta}
                </MenuItem>
              ))}
            </TextField>
            <TextField select label="Regime fiscale" value={regimeFiscale} onChange={(e) => setRegimeFiscale(e.target.value)} fullWidth disabled={inCorso}>
              {Object.entries(ETICHETTA_REGIME).map(([valore, etichetta]) => (
                <MenuItem key={valore} value={valore}>
                  {etichetta}
                </MenuItem>
              ))}
            </TextField>
            <TextField label="Divisa" value={divisa} onChange={(e) => setDivisa(e.target.value)} sx={{ width: mobile ? '100%' : 120, flexShrink: 0 }} disabled={inCorso} />
          </Box>

          <Divider />
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>Righe</Typography>
          {senzaIva && (
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary, mt: -1 }}>
              Regime senza IVA: tutte le righe sono a 0% con natura N2.2, in automatico.
            </Typography>
          )}
          {!modifica && proposta.isLoading && <Skeleton variant="rounded" height={70} />}
          {righe.map((r, i) => (
            <Box
              key={r.chiave}
              sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, p: 1.5, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5 }}
            >
              <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
                <TextField
                  label={`Descrizione riga ${i + 1}`}
                  size="small"
                  value={r.descrizione}
                  onChange={(e) => aggiornaRiga(r.chiave, { descrizione: e.target.value })}
                  disabled={inCorso}
                  multiline
                  fullWidth
                  slotProps={{ htmlInput: { maxLength: 1000 } }}
                />
                <IconButton size="small" aria-label={`Togli la riga ${i + 1}`} onClick={() => setRigheModificate(righe.filter((x) => x.chiave !== r.chiave))} disabled={inCorso}>
                  <CloseIcon fontSize="small" />
                </IconButton>
              </Box>
              <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
                <TextField
                  label="Quantità"
                  type="number"
                  size="small"
                  value={r.quantita}
                  onChange={(e) => aggiornaRiga(r.chiave, { quantita: e.target.value })}
                  disabled={inCorso}
                  sx={{ width: 100 }}
                />
                <TextField
                  label="Prezzo unitario (€)"
                  type="number"
                  size="small"
                  value={r.prezzoUnitario}
                  onChange={(e) => aggiornaRiga(r.chiave, { prezzoUnitario: e.target.value })}
                  disabled={inCorso}
                  sx={{ width: 150 }}
                />
                {/* Su una ricevuta di locazione breve l'IVA non esiste: niente da scegliere. */}
                {!ricevuta && (
                  <TextField
                    select
                    label="IVA"
                    size="small"
                    value={r.iva}
                    onChange={(e) => aggiornaRiga(r.chiave, { iva: e.target.value })}
                    disabled={inCorso || senzaIva}
                    sx={{ width: mobile ? '100%' : 220 }}
                  >
                    {ALIQUOTE_POSITIVE.map((a) => (
                      <MenuItem key={a} value={`a${a}`}>
                        {a}%
                      </MenuItem>
                    ))}
                    {NATURE.map((n) => (
                      <MenuItem key={n} value={`n${n}`}>
                        0% — {ETICHETTA_NATURA[n]}
                      </MenuItem>
                    ))}
                  </TextField>
                )}
                <Typography sx={{ fontSize: 13, fontWeight: 600, ml: 'auto' }}>{formattatoreEuro.format(totaliRiga[i] / 100)}</Typography>
              </Box>
            </Box>
          ))}
          <Box>
            <Button
              size="small"
              variant="outlined"
              onClick={() =>
                setRigheModificate([
                  ...righe,
                  {
                    chiave: `n${prossimaChiave.current++}`,
                    descrizione: '',
                    quantita: '1',
                    prezzoUnitario: '',
                    iva: ricevuta ? '' : senzaIva ? IVA_SENZA_IVA : ivaPredefinita,
                    tipo: TipoRigaFattura.Altro,
                    prenotazioneId: null,
                    prenotazioneServizioId: null,
                  },
                ])
              }
              disabled={inCorso}
            >
              Aggiungi riga
            </Button>
          </Box>

          <TextField
            label="Imposta di soggiorno"
            type="number"
            value={impostaSoggiorno}
            onChange={(e) => setImpostaSoggiornoModificata(e.target.value)}
            disabled={inCorso}
            helperText="Riga a sé in fattura, esclusa dall'IVA (art. 15 DPR 633/72): non va sommata al prezzo del soggiorno. Vuoto se l'ospite non la paga qui."
          />

          <Box sx={{ alignSelf: 'flex-end', width: mobile ? '100%' : 300, display: 'flex', flexDirection: 'column', gap: 0.5 }}>
            <RigaTotale etichetta={ricevuta ? 'Corrispettivo' : 'Imponibile'} valore={imponibile} />
            {!ricevuta &&
              (ivaPerAliquota.length > 1 ? (
                ivaPerAliquota.map((r) => <RigaTotale key={r.aliquota} etichetta={`IVA ${r.aliquota}% su ${formattatoreEuro.format(r.base / 100)}`} valore={r.iva} />)
              ) : (
                <RigaTotale etichetta="IVA" valore={iva} />
              ))}
            {tassa > 0 && <RigaTotale etichetta="Imposta di soggiorno" valore={tassa} />}
            <Divider sx={{ my: 0.5 }} />
            <RigaTotale etichetta="Totale" valore={totale} forte />
          </Box>

          {modifica?.importoBollo != null && (
            <Alert severity="info">
              Questa fattura sconta l&apos;imposta di bollo di {formattatoreEuro.format(modifica.importoBollo)}: le somme senza IVA superano 77,47 €.
              Viene dichiarata nel file per lo SdI e stampata sul PDF.
            </Alert>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button onClick={onClose} disabled={inCorso}>
            Chiudi
          </Button>
          <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
            {modifica ? 'Salva modifiche' : ricevuta ? 'Crea ricevuta' : 'Crea fattura'}
          </Button>
        </DialogActions>
      </Dialog>

      {clienteDaCompletare && (
        <DatiClienteDialog strutturaId={strutturaId} cliente={clienteDaCompletare} clienteNonPersistito onClose={() => setClienteDaCompletare(null)} />
      )}
    </>
  )
}

function RigaTotale({ etichetta, valore, forte = false }: { etichetta: string; valore: number; forte?: boolean }) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}>
      <Typography sx={{ fontSize: forte ? 14 : 13, color: forte ? undefined : tokens.textSecondary, fontWeight: forte ? 700 : 400 }}>{etichetta}</Typography>
      <Typography sx={{ fontSize: forte ? 15 : 13, fontWeight: forte ? 700 : 600 }}>{formattatoreEuro.format(valore / 100)}</Typography>
    </Box>
  )
}
