import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { ApiError } from '../api/client'
import { useAggiornaImpostazioni, useImpostazioni, type ImpostazioniStrutturaDto, type ImpostazioniStrutturaRequest } from '../api/impostazioni'
import { fontDisplay, tokens } from '../theme'

export function ImpostazioniPage() {
  const { strutturaId } = useStruttura()
  const impostazioni = useImpostazioni(strutturaId)

  return (
    <Box sx={{ maxWidth: 640 }}>
      {impostazioni.isLoading && <Skeleton variant="rounded" height={380} />}
      {!impostazioni.isLoading && impostazioni.data && <ImpostazioniForm strutturaId={strutturaId!} dati={impostazioni.data} />}
    </Box>
  )
}

function ImpostazioniForm({ strutturaId, dati }: { strutturaId: string; dati: ImpostazioniStrutturaDto }) {
  const [poliziaStatoAttiva, setPoliziaStatoAttiva] = useState(dati.poliziaStatoAttiva)
  const [osservatorioAttivo, setOsservatorioAttivo] = useState(dati.osservatorioAttivo)
  const [payTouristAttivo, setPayTouristAttivo] = useState(dati.payTouristAttivo)
  const [oraInvioGiornaliero, setOraInvioGiornaliero] = useState(dati.oraInvioGiornaliero?.slice(0, 5) ?? '04:00')
  const [tassaSoggiornoPrezzo, setTassaSoggiornoPrezzo] = useState(dati.tassaSoggiornoPrezzo != null ? String(dati.tassaSoggiornoPrezzo) : '')
  const [tassaSoggiornoMaxGiorni, setTassaSoggiornoMaxGiorni] = useState(dati.tassaSoggiornoMaxGiorni != null ? String(dati.tassaSoggiornoMaxGiorni) : '')
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
    }

    aggiorna.mutate(request, {
      onSuccess: () => setSalvato(true),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Invii automatici</Typography>
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
          Attiva qui i servizi per cui hai già configurato le credenziali nelle rispettive sezioni. L'orario si applica a tutti gli invii
          giornalieri di questa struttura.
        </Typography>

        {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
        {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Impostazioni salvate.</Alert>}

        <FormControlLabel
          control={<Checkbox checked={poliziaStatoAttiva} onChange={(e) => setPoliziaStatoAttiva(e.target.checked)} disabled={aggiorna.isPending} />}
          label="Invio schedine Polizia di Stato (Alloggiati Web)"
        />
        <FormControlLabel
          control={<Checkbox checked={osservatorioAttivo} onChange={(e) => setOsservatorioAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
          label="Invio Osservatorio Turistico"
        />
        <FormControlLabel
          control={<Checkbox checked={payTouristAttivo} onChange={(e) => setPayTouristAttivo(e.target.checked)} disabled={aggiorna.isPending} />}
          label="Invio PayTourist"
        />

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
      </Box>

      <Box>
        <Button variant="contained" color="secondary" onClick={salva} disabled={aggiorna.isPending}>
          Salva impostazioni
        </Button>
      </Box>
    </Box>
  )
}
