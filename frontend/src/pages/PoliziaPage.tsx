import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import { ApiError } from '../api/client'
import {
  esportaSchedineAlloggiatiWeb,
  useAggiornaAlloggiatiWebConfig,
  useAlloggiatiWebConfig,
  useInviaAlloggiatiWebOra,
  type AlloggiatiWebIntegrazioneDto,
} from '../api/integrazioni'
import { fontDisplay, fontMono, tokens } from '../theme'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function PoliziaPage() {
  const { strutturaId } = useStruttura()
  const config = useAlloggiatiWebConfig(strutturaId)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 760 }}>
      <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
        Invio giornaliero delle schedine di soggiorno alla Polizia di Stato (Alloggiati Web). L'orario di invio automatico si imposta in
        Impostazioni.
      </Typography>

      {config.isLoading && <Skeleton variant="rounded" height={340} />}
      {!config.isLoading && config.data && <ConfigForm strutturaId={strutturaId!} dati={config.data} />}
    </Box>
  )
}

function ConfigForm({ strutturaId, dati }: { strutturaId: string; dati: AlloggiatiWebIntegrazioneDto }) {
  const [utente, setUtente] = useState(dati.utente ?? '')
  const [password, setPassword] = useState('')
  const [wsKey, setWsKey] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvato, setSalvato] = useState(false)
  const [risultatoInvio, setRisultatoInvio] = useState<{ inviate: number; totale: number; errori: string[]; messaggio: string | null } | null>(null)

  const aggiorna = useAggiornaAlloggiatiWebConfig(strutturaId)
  const invia = useInviaAlloggiatiWebOra(strutturaId)

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

  function inviaOra() {
    setErrore(null)
    setRisultatoInvio(null)
    invia.mutate(undefined, {
      onSuccess: (r) => setRisultatoInvio({ inviate: r.inviate, totale: r.totaleSchedine, errori: r.errori, messaggio: r.messaggio }),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  async function esporta() {
    try {
      await esportaSchedineAlloggiatiWeb(strutturaId)
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Download non riuscito.')
    }
  }

  return (
    <>
      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Credenziali Alloggiati Web</Typography>
          <StatoBadge configurato={dati.credenzialiConfigurate} />
        </Box>

        {errore && <Alert severity="error" onClose={() => setErrore(null)}>{errore}</Alert>}
        {salvato && !errore && <Alert severity="success" onClose={() => setSalvato(false)}>Credenziali salvate.</Alert>}

        <TextField label="Utente" value={utente} onChange={(e) => setUtente(e.target.value)} disabled={aggiorna.isPending} />
        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            fullWidth
            disabled={aggiorna.isPending}
            helperText={dati.credenzialiConfigurate ? "Già salvata: lasciarla vuota e salvare la AZZERA" : ' '}
          />
          <TextField
            label="Ws Key"
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

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Stato invii</Typography>

        <Box sx={{ display: 'flex', gap: 4 }}>
          <Campo etichetta="Ultimo invio" valore={dati.ultimoInvioAtUtc ? formattatoreDataOra.format(new Date(dati.ultimoInvioAtUtc)) : 'mai eseguito'} />
          <Campo etichetta="Schedine nell'ultimo invio" valore={String(dati.ultimeSchedineInviate ?? '—')} />
        </Box>

        {dati.ultimoErrore && (
          <Alert severity="warning" sx={{ mt: 0.5 }}>
            {dati.ultimoErrore}
          </Alert>
        )}

        {risultatoInvio && (
          <Alert severity={risultatoInvio.errori.length > 0 ? 'warning' : 'success'} sx={{ mt: 0.5 }} onClose={() => setRisultatoInvio(null)}>
            {risultatoInvio.inviate}/{risultatoInvio.totale} schedine inviate.
            {risultatoInvio.messaggio ? ` ${risultatoInvio.messaggio}` : ''}
            {risultatoInvio.errori.length > 0 && ` Errori: ${risultatoInvio.errori.join('; ')}`}
          </Alert>
        )}

        <Box sx={{ display: 'flex', gap: 1.5, mt: 0.5 }}>
          <Button variant="contained" color="secondary" onClick={inviaOra} disabled={invia.isPending}>
            Invia ora
          </Button>
          <Button variant="outlined" onClick={esporta}>
            Esporta schedine del giorno
          </Button>
        </Box>
      </Box>
    </>
  )
}

function Campo({ etichetta, valore }: { etichetta: string; valore: string }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.04em' }}>{etichetta}</Typography>
      <Typography sx={{ fontFamily: fontMono, fontSize: 14, fontWeight: 600 }}>{valore}</Typography>
    </Box>
  )
}

function StatoBadge({ configurato }: { configurato: boolean }) {
  return (
    <Box
      sx={{
        fontSize: 11.5,
        fontWeight: 700,
        color: '#fff',
        bgcolor: configurato ? tokens.ok600 : tokens.textTertiary,
        borderRadius: 999,
        px: 1.25,
        py: 0.375,
      }}
    >
      {configurato ? 'Configurato' : 'Non configurato'}
    </Box>
  )
}
