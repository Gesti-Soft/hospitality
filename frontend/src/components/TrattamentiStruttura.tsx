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
import { useMobile } from '../lib/useMobile'
import { fontDisplay, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { SceltaEuroPercentuale } from './SceltaEuroPercentuale'

const TIPI: TipoTrattamento[] = [TipoTrattamento.Colazione, TipoTrattamento.MezzaPensione, TipoTrattamento.PensioneCompleta]

/**
 * Trattamenti della struttura: prezzo a persona e a notte, che si somma a quello della camera.
 * Facoltativi: finché non sono attivi, nella prenotazione resta solo il pernottamento.
 */
export function TrattamentiStruttura({ strutturaId, puoScrivere }: { strutturaId: string; puoScrivere: boolean }) {
  const trattamenti = useTrattamenti(strutturaId)

  if (trattamenti.isLoading) return <Skeleton variant="rounded" height={260} />

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 760 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Prezzi a persona e a notte, in aggiunta al prezzo della camera. Si scelgono nella prenotazione; il prezzo resta quello del momento
        in cui la prenotazione è stata fatta. Non vengono inviati all&apos;OTA.
      </Typography>
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
  const mobile = useMobile()
  const toast = useToast()
  const salva = useSalvaTrattamento(strutturaId)
  const [attivo, setAttivo] = useState(salvato?.attivo ?? false)
  const [prezzo, setPrezzo] = useState(salvato ? String(salvato.prezzoPerPersona) : '')
  const [prezzoBambini, setPrezzoBambini] = useState(salvato?.prezzoBambini != null ? String(salvato.prezzoBambini) : '')
  const [tipoPrezzoBambini, setTipoPrezzoBambini] = useState<TipoVariazionePrezzo>(salvato?.tipoPrezzoBambini ?? TipoVariazionePrezzo.Euro)
  const [etaMassima, setEtaMassima] = useState(salvato?.etaMassimaBambini != null ? String(salvato.etaMassimaBambini) : '')
  const [esercizio, setEsercizio] = useState(salvato?.esercizioConvenzionato ?? '')
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

      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 1.5, alignItems: mobile ? 'stretch' : 'center' }}>
        <TextField
          label="Prezzo a persona (€/notte)"
          type="number"
          size="small"
          value={prezzo}
          onChange={(e) => setPrezzo(e.target.value)}
          disabled={disabilitato}
          slotProps={{ htmlInput: { min: 0 } }}
          sx={{ flex: 1 }}
        />
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flex: 1 }}>
          <TextField
            label={inPercentuale ? 'Bambini (% dell’adulto)' : 'Bambini (€/notte)'}
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
          sx={{ width: mobile ? 'auto' : 150 }}
        />
      </Box>
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Prezzo bambini facoltativo: vuoto = pagano come gli adulti. 0 = gratis (es. fino a 2 anni).
      </Typography>

      {tipo === TipoTrattamento.Colazione && (
        <TextField
          label="Bar o esercizio convenzionato (facoltativo)"
          size="small"
          value={esercizio}
          onChange={(e) => setEsercizio(e.target.value)}
          disabled={disabilitato}
          helperText="Se la colazione la serve un bar convenzionato: compare sui buoni colazione da consegnare all'ospite."
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
