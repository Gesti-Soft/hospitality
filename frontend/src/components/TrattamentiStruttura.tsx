import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { TipoVariazionePrezzo } from '../api/tipologie'
import { NOME_TRATTAMENTO, TipoTrattamento, useSalvaTrattamento, useTrattamenti, type TrattamentoStrutturaDto } from '../api/trattamenti'
import { fontDisplay, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { SceltaEuroPercentuale } from './SceltaEuroPercentuale'

const TIPI: TipoTrattamento[] = [TipoTrattamento.Colazione, TipoTrattamento.MezzaPensione, TipoTrattamento.PensioneCompleta, TipoTrattamento.AllInclusive]

/**
 * Trattamenti della struttura: prezzo a persona e a notte, che si somma a quello della camera.
 * Facoltativi: finché non sono attivi, nella prenotazione resta solo il pernottamento.
 */
export function TrattamentiStruttura({ strutturaId, puoScrivere }: { strutturaId: string; puoScrivere: boolean }) {
  const trattamenti = useTrattamenti(strutturaId)

  if (trattamenti.isLoading) return <Skeleton variant="rounded" height={260} />

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Box sx={{ maxWidth: 760 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 17 }}>Trattamenti</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary, mt: 0.5 }}>
          Prezzi a persona e a notte, in aggiunta al prezzo della camera. Uno per prenotazione; il prezzo resta quello del momento in cui
          la prenotazione è stata fatta. Non vengono inviati all&apos;OTA.
        </Typography>
      </Box>
      {/* Tante schede per riga quante ne stanno (4 su uno schermo largo, una sul telefono). */}
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(100%, 380px), 1fr))', gap: 2, alignItems: 'start' }}>
        {TIPI.map((tipo) => (
          <SchedaTrattamento
            // Nasce a listino già caricato (sopra si aspetta il caricamento) e poi tiene il suo stato:
            // salvarne una non deve cancellare quello che si sta scrivendo nelle altre.
            key={tipo}
            strutturaId={strutturaId}
            tipo={tipo}
            salvato={trattamenti.data?.find((t) => t.tipo === tipo) ?? null}
            puoScrivere={puoScrivere}
          />
        ))}
      </Box>
    </Box>
  )
}

function SchedaTrattamento({
  strutturaId,
  tipo,
  salvato,
  puoScrivere,
}: {
  strutturaId: string
  tipo: TipoTrattamento
  salvato: TrattamentoStrutturaDto | null
  puoScrivere: boolean
}) {
  const toast = useToast()
  const salva = useSalvaTrattamento(strutturaId)
  const [attivo, setAttivo] = useState(salvato?.attivo ?? false)
  const [prezzo, setPrezzo] = useState(salvato ? String(salvato.prezzoPerPersona) : '')
  const [prezzoBambini, setPrezzoBambini] = useState(salvato?.prezzoBambini != null ? String(salvato.prezzoBambini) : '')
  const [tipoPrezzoBambini, setTipoPrezzoBambini] = useState<TipoVariazionePrezzo>(salvato?.tipoPrezzoBambini ?? TipoVariazionePrezzo.Euro)
  const [etaMassima, setEtaMassima] = useState(salvato?.etaMassimaBambini != null ? String(salvato.etaMassimaBambini) : '')
  const [esercizio, setEsercizio] = useState(salvato?.esercizioConvenzionato ?? '')
  const [stampaTicket, setStampaTicket] = useState(salvato?.stampaTicket ?? false)
  const disabilitato = !puoScrivere || salva.isPending

  function salvaScheda() {
    if (attivo && prezzo.trim() === '') {
      toast.errore(`Indica il prezzo a persona di "${NOME_TRATTAMENTO[tipo]}".`)
      return
    }
    salva.mutate(
      {
        tipo,
        attivo,
        prezzoPerPersona: Number(prezzo) || 0,
        prezzoBambini: prezzoBambini.trim() === '' ? null : Number(prezzoBambini),
        tipoPrezzoBambini,
        etaMassimaBambini: etaMassima.trim() === '' ? null : Number(etaMassima),
        esercizioConvenzionato: esercizio.trim() === '' ? null : esercizio.trim(),
        stampaTicket,
      },
      {
        onSuccess: () => toast.successo(`${NOME_TRATTAMENTO[tipo]} salvato.`),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  const inPercentuale = tipoPrezzoBambini === TipoVariazionePrezzo.Percentuale

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.5, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>{NOME_TRATTAMENTO[tipo]}</Typography>
        <FormControlLabel
          control={<Checkbox checked={attivo} onChange={(e) => setAttivo(e.target.checked)} disabled={disabilitato} />}
          label="Offerto"
          sx={{ mr: 0 }}
        />
      </Box>

      {/* Un campo per riga: le schede sono affiancate e strette, in riga le etichette si tagliavano. */}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <TextField
          label="Prezzo a persona (€/notte)"
          type="number"
          size="small"
          value={prezzo}
          onChange={(e) => setPrezzo(e.target.value)}
          disabled={disabilitato}
          slotProps={{ htmlInput: { min: 0 } }}
          fullWidth
        />
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
          <TextField
            label={inPercentuale ? 'Bambini a notte (% dell’adulto)' : 'Bambini a notte (€)'}
            type="number"
            size="small"
            value={prezzoBambini}
            onChange={(e) => setPrezzoBambini(e.target.value)}
            disabled={disabilitato}
            slotProps={{ htmlInput: { min: 0, max: inPercentuale ? 100 : undefined } }}
            sx={{ flex: 1 }}
          />
          <SceltaEuroPercentuale valore={tipoPrezzoBambini} onChange={setTipoPrezzoBambini} disabled={disabilitato} />
        </Box>
        <TextField
          label="Bambini fino a (anni)"
          type="number"
          size="small"
          value={etaMassima}
          onChange={(e) => {
            // Solo cifre, oltre 17 diventa 17: dai 18 anni si è adulti.
            const v = e.target.value
            if (v === '' || /^\d+$/.test(v)) setEtaMassima(v === '' ? '' : String(Math.min(Number(v), 17)))
          }}
          disabled={disabilitato || prezzoBambini.trim() === ''}
          slotProps={{ htmlInput: { min: 0, max: 17 } }}
          fullWidth
        />
      </Box>
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Prezzo bambini facoltativo: vuoto = pagano come gli adulti. 0 = gratis (es. fino a 2 anni).
      </Typography>

      <FormControlLabel
        control={<Checkbox checked={stampaTicket} onChange={(e) => setStampaTicket(e.target.checked)} disabled={disabilitato} />}
        label="Stampa ticket"
      />
      {stampaTicket && (
        <TextField
          label="Esercizio convenzionato (facoltativo)"
          size="small"
          value={esercizio}
          onChange={(e) => setEsercizio(e.target.value)}
          disabled={disabilitato}
          helperText="Se il trattamento lo serve un esercizio convenzionato: compare sui ticket da consegnare all'ospite."
        />
      )}

      {puoScrivere && (
        <Box>
          <Button variant="outlined" size="small" onClick={salvaScheda} disabled={salva.isPending}>
            Salva
          </Button>
        </Box>
      )}
    </Box>
  )
}
