import { useEffect, useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import type { PrenotazioneDto } from '../api/prenotazioni'
import {
  AliquotaIva,
  NaturaIva,
  RegimeFiscale,
  TipoDocumentoFattura,
  useAggiornaFattura,
  useCreaFattura,
  useDatiAziendali,
  useRisolviClientePerPrenotazione,
  type AggiornaFatturaRequest,
  type CreaFatturaDaPrenotazioneRequest,
  type DatiClienteDto,
  type DatiFatturaDto,
} from '../api/fatturazione'
import { useMobile } from '../lib/useMobile'
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

export function FatturaDialog({ strutturaId, stato, prenotazioniDisponibili, clienti, onClose }: Props) {
  const mobile = useMobile()
  const modifica = stato.modo === 'modifica' ? stato.fattura : null

  // Se c'è una sola prenotazione disponibile (es. aperto da "Genera fattura" sulla scheda ospiti di
  // una prenotazione specifica), è già preselezionata: non ha senso farla ricercare di nuovo.
  const [prenotazioneId, setPrenotazioneId] = useState(prenotazioniDisponibili.length === 1 ? prenotazioniDisponibili[0].id : '')
  const [datiClienteId, setDatiClienteId] = useState(modifica?.datiClienteId ?? '')
  const [tipoDocumento, setTipoDocumento] = useState<string>(modifica?.tipoDocumento != null ? String(modifica.tipoDocumento) : String(TipoDocumentoFattura.TD01_Fattura))
  const [regimeFiscale, setRegimeFiscale] = useState<string>(modifica?.regimeFiscale != null ? String(modifica.regimeFiscale) : String(RegimeFiscale.RF01_Ordinario))
  // Nessun testo proposto: la descrizione è quello che il cliente si ritrova scritto in fattura,
  // e "Soggiorno" precompilato veniva confermato senza leggerlo. Va scritta ogni volta.
  const [descrizione, setDescrizione] = useState(modifica?.descrizione ?? '')
  const [quantita, setQuantita] = useState(String(modifica?.quantita ?? 1))
  const [prezzoUnitario, setPrezzoUnitario] = useState(modifica?.prezzoUnitario != null ? String(modifica.prezzoUnitario) : '')
  // Finché l'operatore non tocca il campo a mano, "Prezzo unitario" segue il prezzo della prenotazione
  // scelta — mai in modifica, dove il prezzo è già quello della fattura esistente, non della prenotazione.
  const [prezzoUnitarioAuto, setPrezzoUnitarioAuto] = useState(!modifica)
  const [aliquotaIva, setAliquotaIva] = useState<string>(modifica?.aliquotaIva != null ? String(modifica.aliquotaIva) : String(AliquotaIva.Iva10))
  const [natura, setNatura] = useState<string>(modifica?.natura != null ? String(modifica.natura) : '')
  const [divisa, setDivisa] = useState(modifica?.divisa ?? 'EUR')
  const [errore, setErrore] = useState<string | null>(null)
  // Cliente fatturabile appena creato (bare-bones) per la prenotazione scelta, da completare con
  // P.IVA/CF/indirizzo/PEC prima di procedere — vedi effect sotto.
  const [clienteDaCompletare, setClienteDaCompletare] = useState<DatiClienteDto | null>(null)

  const crea = useCreaFattura(strutturaId)
  const aggiorna = useAggiornaFattura(strutturaId)
  const risolviCliente = useRisolviClientePerPrenotazione(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  // Regime fiscale, aliquota e natura di una fattura nuova arrivano dal profilo fiscale della
  // struttura: dipendono da chi emette, non dalla singola fattura, e prima erano valori fissi nel
  // codice (RF01 e 10%) che un forfettario doveva correggere a ogni fattura. Si applicano una volta
  // sola, quando i dati arrivano, e mai in modifica — lì contano i valori con cui è stata emessa.
  const datiAziendali = useDatiAziendali(strutturaId)
  const predefinitiApplicati = useRef(false)
  useEffect(() => {
    if (modifica !== null || predefinitiApplicati.current || !datiAziendali.data) return
    predefinitiApplicati.current = true

    if (datiAziendali.data.regimeFiscale != null) setRegimeFiscale(String(datiAziendali.data.regimeFiscale))
    if (datiAziendali.data.aliquotaIvaDefault != null) setAliquotaIva(String(datiAziendali.data.aliquotaIvaDefault))
    if (datiAziendali.data.naturaDefault != null) setNatura(String(datiAziendali.data.naturaDefault))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [datiAziendali.data])

  // Ogni volta che si sceglie una prenotazione (mai in modifica, dove il Cliente è già assegnato),
  // chiede subito un'ANTEPRIMA (nessuna scrittura) del Cliente fatturabile collegato al suo ospite —
  // lo stesso che userebbe comunque "Crea fattura" — invece di lasciarlo scoprire solo alla fine. Se
  // non esiste ancora, il form "Nuovo cliente" si apre già precompilato ma NON crea nulla finché
  // l'operatore non preme "Crea cliente" lì dentro (clienteNonPersistito) — così annullare senza
  // salvare non lascia un Cliente bare-bones orfano nel database.
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

  // Propone l'importo totale della prenotazione scelta (preventivo camera + eventuali spese di
  // pulizia/animali/cauzione già decise in fase di prenotazione) — MAI la tassa di soggiorno, che
  // non è soggetta a IVA come il soggiorno e va gestita a parte (vedi l'Alert qui sotto quando
  // presente). Si ferma non appena l'operatore modifica il campo a mano.
  useEffect(() => {
    if (!prezzoUnitarioAuto) return
    const p = prenotazioniDisponibili.find((x) => x.id === prenotazioneId)
    setPrezzoUnitario(p?.importoTotale != null ? String(p.importoTotale) : '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prenotazioneId, prezzoUnitarioAuto])

  function salva() {
    if (!modifica && prenotazioneId === '') {
      setErrore('Seleziona la prenotazione da fatturare.')
      return
    }
    if (descrizione.trim() === '') {
      setErrore('Scrivi la descrizione: è la riga che comparirà in fattura.')
      return
    }
    if (Number(quantita) <= 0) {
      setErrore('La quantità deve essere maggiore di zero.')
      return
    }
    setErrore(null)

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (modifica) {
      const request: AggiornaFatturaRequest = {
        datiClienteId: datiClienteId === '' ? null : datiClienteId,
        tipoDocumento: Number(tipoDocumento) as TipoDocumentoFattura,
        regimeFiscale: Number(regimeFiscale) as RegimeFiscale,
        descrizione: descrizione.trim() === '' ? null : descrizione.trim(),
        quantita: Number(quantita),
        prezzoUnitario: Number(prezzoUnitario) || 0,
        aliquotaIva: aliquotaIva === '' ? null : (Number(aliquotaIva) as AliquotaIva),
        natura: natura === '' ? null : (Number(natura) as NaturaIva),
        divisa: divisa.trim() === '' ? null : divisa.trim(),
      }
      aggiorna.mutate({ fatturaId: modifica.id, request }, { onSuccess: onClose, onError })
    } else {
      const request: CreaFatturaDaPrenotazioneRequest = {
        prenotazioneId,
        tipoDocumento: Number(tipoDocumento) as TipoDocumentoFattura,
        regimeFiscale: Number(regimeFiscale) as RegimeFiscale,
        descrizione: descrizione.trim() === '' ? null : descrizione.trim(),
        quantita: Number(quantita),
        prezzoUnitario: prezzoUnitario.trim() === '' ? null : Number(prezzoUnitario),
        aliquotaIva: aliquotaIva === '' ? null : (Number(aliquotaIva) as AliquotaIva),
        natura: natura === '' ? null : (Number(natura) as NaturaIva),
        divisa: divisa.trim() === '' ? null : divisa.trim(),
      }
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  return (
    <>
      <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{modifica ? `Modifica fattura n. ${modifica.numeroDocumento}` : 'Nuova fattura da prenotazione'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        {!modifica && (
          <Autocomplete
            options={prenotazioniDisponibili}
            getOptionLabel={etichettaPrenotazione}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            value={prenotazioniDisponibili.find((p) => p.id === prenotazioneId) ?? null}
            onChange={(_, v) => setPrenotazioneId(v?.id ?? '')}
            disabled={inCorso}
            noOptionsText="Nessuna prenotazione disponibile"
            renderInput={(params) => <TextField {...params} label="Prenotazione da fatturare" required placeholder="Cerca per numero o ospite..." />}
          />
        )}

        {!modifica &&
          prenotazioneId !== '' &&
          (() => {
            const p = prenotazioniDisponibili.find((x) => x.id === prenotazioneId)
            return p?.totalTax ? (
              <Alert severity="info">
                Questa prenotazione ha una tassa di soggiorno di {p.totalTax.toLocaleString('it-IT', { style: 'currency', currency: 'EUR' })}, non
                inclusa automaticamente nel prezzo qui sotto (non è soggetta a IVA come il soggiorno): aggiungila manualmente all'importo o in una
                riga/nota separata se vuoi fatturarla insieme.
              </Alert>
            ) : null
          })()}

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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
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
        </Box>

        <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} required disabled={inCorso} />

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', flexWrap: 'wrap', gap: 2 }}>
          <TextField
            label="Quantità"
            type="number"
            value={quantita}
            onChange={(e) => setQuantita(e.target.value)}
            sx={{ width: mobile ? '100%' : 120, flexShrink: 0 }}
            disabled={inCorso}
          />
          <TextField
            label="Prezzo unitario (€)"
            type="number"
            value={prezzoUnitario}
            onChange={(e) => {
              setPrezzoUnitario(e.target.value)
              setPrezzoUnitarioAuto(false)
            }}
            sx={{ flex: mobile ? undefined : '1 1 220px', width: mobile ? '100%' : undefined }}
            disabled={inCorso}         
          />
          {/* Quantità e Divisa non si comprimono mai (flexShrink 0, prima la Divisa veniva
              schiacciata fino a tagliarne label e valore): se il Prezzo scende sotto la sua base
              il flexWrap manda la Divisa a capo, invece di stringerla. */}
          <TextField
            label="Divisa"
            value={divisa}
            onChange={(e) => setDivisa(e.target.value)}
            sx={{ width: mobile ? '100%' : 120, flexShrink: 0 }}
            disabled={inCorso}
          />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField select label="Aliquota IVA" value={aliquotaIva} onChange={(e) => setAliquotaIva(e.target.value)} fullWidth disabled={inCorso}>
            {Object.entries(AliquotaIva).map(([nome, valore]) => (
              <MenuItem key={nome} value={String(valore)}>
                {valore}%
              </MenuItem>
            ))}
          </TextField>
          {aliquotaIva === String(AliquotaIva.Iva0) && (
            <TextField select label="Natura IVA" value={natura} onChange={(e) => setNatura(e.target.value)} fullWidth disabled={inCorso}>
              <MenuItem value="">—</MenuItem>
              {Object.entries(ETICHETTA_NATURA).map(([valore, etichetta]) => (
                <MenuItem key={valore} value={valore}>
                  {etichetta}
                </MenuItem>
              ))}
            </TextField>
          )}
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {modifica ? 'Salva modifiche' : 'Crea fattura'}
        </Button>
      </DialogActions>
    </Dialog>

    {clienteDaCompletare && (
      <DatiClienteDialog
        strutturaId={strutturaId}
        cliente={clienteDaCompletare}
        clienteNonPersistito
        onClose={() => setClienteDaCompletare(null)}
      />
    )}
    </>
  )
}
