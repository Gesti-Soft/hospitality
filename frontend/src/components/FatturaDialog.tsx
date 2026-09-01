import { useState } from 'react'
import Alert from '@mui/material/Alert'
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
  type AggiornaFatturaRequest,
  type CreaFatturaDaPrenotazioneRequest,
  type DatiClienteDto,
  type DatiFatturaDto,
} from '../api/fatturazione'

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

const ETICHETTA_NATURA: Record<NaturaIva, string> = {
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

export function FatturaDialog({ strutturaId, stato, prenotazioniDisponibili, clienti, onClose }: Props) {
  const modifica = stato.modo === 'modifica' ? stato.fattura : null

  const [prenotazioneId, setPrenotazioneId] = useState('')
  const [datiClienteId, setDatiClienteId] = useState(modifica?.datiClienteId ?? '')
  const [tipoDocumento, setTipoDocumento] = useState<string>(modifica?.tipoDocumento != null ? String(modifica.tipoDocumento) : String(TipoDocumentoFattura.TD01_Fattura))
  const [regimeFiscale, setRegimeFiscale] = useState<string>(modifica?.regimeFiscale != null ? String(modifica.regimeFiscale) : String(RegimeFiscale.RF01_Ordinario))
  const [descrizione, setDescrizione] = useState(modifica?.descrizione ?? 'Soggiorno')
  const [quantita, setQuantita] = useState(String(modifica?.quantita ?? 1))
  const [prezzoUnitario, setPrezzoUnitario] = useState(modifica?.prezzoUnitario != null ? String(modifica.prezzoUnitario) : '')
  const [aliquotaIva, setAliquotaIva] = useState<string>(modifica?.aliquotaIva != null ? String(modifica.aliquotaIva) : String(AliquotaIva.Iva10))
  const [natura, setNatura] = useState<string>(modifica?.natura != null ? String(modifica.natura) : '')
  const [divisa, setDivisa] = useState(modifica?.divisa ?? 'EUR')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaFattura(strutturaId)
  const aggiorna = useAggiornaFattura(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  function salva() {
    if (!modifica && prenotazioneId === '') {
      setErrore('Seleziona la prenotazione da fatturare.')
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
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{modifica ? `Modifica fattura n. ${modifica.numeroDocumento}` : 'Nuova fattura da prenotazione'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        {!modifica && (
          <TextField select label="Prenotazione da fatturare" value={prenotazioneId} onChange={(e) => setPrenotazioneId(e.target.value)} required disabled={inCorso}>
            {prenotazioniDisponibili.length === 0 && <MenuItem value="">Nessuna prenotazione disponibile</MenuItem>}
            {prenotazioniDisponibili.map((p) => (
              <MenuItem key={p.id} value={p.id}>
                {p.numeroPrenotazione ? `#${p.numeroPrenotazione}` : p.id.slice(0, 8)} — {p.cameraNome ?? '—'} —{' '}
                {p.checkIn ? new Date(p.checkIn).toLocaleDateString('it-IT') : '—'}
              </MenuItem>
            ))}
          </TextField>
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

        <Box sx={{ display: 'flex', gap: 2 }}>
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

        <TextField label="Descrizione" value={descrizione} onChange={(e) => setDescrizione(e.target.value)} disabled={inCorso} />

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Quantità" type="number" value={quantita} onChange={(e) => setQuantita(e.target.value)} fullWidth disabled={inCorso} />
          <TextField
            label="Prezzo unitario (€)"
            type="number"
            value={prezzoUnitario}
            onChange={(e) => setPrezzoUnitario(e.target.value)}
            fullWidth
            disabled={inCorso}
            helperText={!modifica ? 'Vuoto = usa l\'importo totale della prenotazione' : ' '}
          />
          <TextField label="Divisa" value={divisa} onChange={(e) => setDivisa(e.target.value)} sx={{ width: 100 }} disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
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
        <Button variant="contained" color="secondary" onClick={salva} disabled={inCorso}>
          {modifica ? 'Salva modifiche' : 'Crea fattura'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
