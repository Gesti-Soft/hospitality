import { useEffect, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import QRCode from 'qrcode'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { attiva2Fa, avvia2Fa, disattiva2Fa, rigeneraCodiciRecupero, stato2Fa } from '../api/auth'
import { ApiError } from '../api/client'
import { useToast } from '../toast/ToastContext'
import { ConfirmDialog } from './ConfirmDialog'
import { fontDisplay, fontMono, tokens } from '../theme'

/**
 * Attivazione e gestione della verifica in due passaggi (TOTP, compatibile con Google
 * Authenticator). Il segreto viene mostrato una volta sola come QR; i codici di recupero
 * altrettanto, subito dopo l'attivazione — da lì in poi sul server restano solo i loro hash,
 * quindi non c'è modo di rileggerli: chi li perde deve rigenerarli.
 */
export function DueFattoriCard() {
  const toast = useToast()
  const queryClient = useQueryClient()
  const stato = useQuery({ queryKey: ['auth', '2fa'], queryFn: stato2Fa })

  const [uriOtpauth, setUriOtpauth] = useState<string | null>(null)
  const [secret, setSecret] = useState<string | null>(null)
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)
  const [codice, setCodice] = useState('')
  const [codiciRecupero, setCodiciRecupero] = useState<string[] | null>(null)
  const [password, setPassword] = useState('')
  const [confermaDisattiva, setConfermaDisattiva] = useState(false)
  const [inCorso, setInCorso] = useState(false)

  useEffect(() => {
    if (!uriOtpauth) {
      setQrDataUrl(null)
      return
    }
    // margin 1 invece del default 4: il QR resta grande dentro un riquadro piccolo.
    QRCode.toDataURL(uriOtpauth, { width: 200, margin: 1 })
      .then(setQrDataUrl)
      .catch(() => setQrDataUrl(null))
  }, [uriOtpauth])

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  async function avvia() {
    setInCorso(true)
    try {
      const avvio = await avvia2Fa()
      setSecret(avvio.secret)
      setUriOtpauth(avvio.uriOtpauth)
      setCodiciRecupero(null)
    } catch (err) {
      gestisciErrore(err)
    } finally {
      setInCorso(false)
    }
  }

  async function attiva() {
    if (codice.trim() === '') {
      toast.errore('Inserisci il codice a 6 cifre.')
      return
    }
    setInCorso(true)
    try {
      const esito = await attiva2Fa(codice.trim())
      setCodiciRecupero(esito.codici)
      setUriOtpauth(null)
      setSecret(null)
      setCodice('')
      await queryClient.invalidateQueries({ queryKey: ['auth', '2fa'] })
      toast.successo('Verifica in due passaggi attivata.')
    } catch (err) {
      gestisciErrore(err)
    } finally {
      setInCorso(false)
    }
  }

  async function disattiva() {
    setInCorso(true)
    try {
      await disattiva2Fa(password)
      setConfermaDisattiva(false)
      setPassword('')
      setCodiciRecupero(null)
      await queryClient.invalidateQueries({ queryKey: ['auth', '2fa'] })
      toast.successo('Verifica in due passaggi disattivata.')
    } catch (err) {
      gestisciErrore(err)
    } finally {
      setInCorso(false)
    }
  }

  async function rigenera() {
    if (password.trim() === '') {
      toast.errore('Inserisci la tua password per rigenerare i codici.')
      return
    }
    setInCorso(true)
    try {
      const esito = await rigeneraCodiciRecupero(password)
      setCodiciRecupero(esito.codici)
      setPassword('')
      await queryClient.invalidateQueries({ queryKey: ['auth', '2fa'] })
      toast.successo('Nuovi codici generati: i precedenti non valgono più.')
    } catch (err) {
      gestisciErrore(err)
    } finally {
      setInCorso(false)
    }
  }

  const attivo = stato.data?.attivo ?? false

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 560 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Verifica in due passaggi</Typography>
        {stato.isLoading ? (
          <Skeleton variant="rounded" width={70} height={24} />
        ) : (
          <Chip
            size="small"
            label={attivo ? 'Attiva' : 'Non attiva'}
            sx={{ bgcolor: attivo ? tokens.ok600 : tokens.surfaceBorder, color: attivo ? '#fff' : tokens.textSecondary, fontWeight: 700 }}
          />
        )}
      </Box>

      <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>
        Oltre alla password, per entrare serve un codice che cambia ogni 30 secondi sul tuo telefono. Chi ruba la password, da
        solo, non entra.
      </Typography>

      {codiciRecupero && (
        <Alert severity="warning" sx={{ '& .MuiAlert-message': { width: '100%' } }}>
          <Typography sx={{ fontSize: 13.5, fontWeight: 700, mb: 1 }}>
            Salva questi codici di recupero adesso: non li rivedrai più.
          </Typography>
          <Typography sx={{ fontSize: 12.5, mb: 1.25 }}>
            Servono per entrare se perdi il telefono. Ognuno vale una volta sola. Tienili dove terresti una chiave di
            riserva, non nello stesso telefono che genera i codici.
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 0.5, fontFamily: fontMono, fontSize: 13.5 }}>
            {codiciRecupero.map((c) => (
              <Box key={c}>{c}</Box>
            ))}
          </Box>
        </Alert>
      )}

      {!attivo && !uriOtpauth && (
        <Box>
          <Button variant="contained" color="primary" onClick={avvia} disabled={inCorso || stato.isLoading}>
            Attiva
          </Button>
        </Box>
      )}

      {!attivo && uriOtpauth && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <Typography sx={{ fontSize: 13.5 }}>
            1. Apri Google Authenticator, tocca <b>+</b> e inquadra questo codice.
          </Typography>
          {qrDataUrl ? (
            <Box component="img" src={qrDataUrl} alt="Codice QR per l'app di autenticazione" sx={{ width: 200, height: 200, alignSelf: 'flex-start', border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1, p: 1, bgcolor: '#fff' }} />
          ) : (
            <Skeleton variant="rounded" width={200} height={200} />
          )}
          {secret && (
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Se non riesci a inquadrarlo, inserisci a mano questa chiave:{' '}
              <Box component="span" sx={{ fontFamily: fontMono, fontSize: 12.5, color: tokens.textSecondary, wordBreak: 'break-all' }}>
                {secret}
              </Box>
            </Typography>
          )}
          <Typography sx={{ fontSize: 13.5 }}>2. Scrivi qui il codice a 6 cifre che compare nell&apos;app.</Typography>
          <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
            <TextField label="Codice" value={codice} onChange={(e) => setCodice(e.target.value)} placeholder="000000" sx={{ width: 160 }} disabled={inCorso} />
            <Button variant="contained" color="primary" onClick={attiva} disabled={inCorso} sx={{ alignSelf: 'center' }}>
              Conferma
            </Button>
            <Button onClick={() => { setUriOtpauth(null); setSecret(null); setCodice('') }} disabled={inCorso} sx={{ alignSelf: 'center' }}>
              Annulla
            </Button>
          </Box>
        </Box>
      )}

      {attivo && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Typography sx={{ fontSize: 13 , color: tokens.textSecondary }}>
            Codici di recupero ancora utilizzabili: <b>{stato.data?.codiciRimasti ?? 0}</b>
          </Typography>
          <TextField
            label="La tua password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            sx={{ maxWidth: 280 }}
            disabled={inCorso}
            helperText="Serve sia per rigenerare i codici sia per disattivare."
          />
          <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
            <Button variant="outlined" onClick={rigenera} disabled={inCorso}>
              Rigenera codici di recupero
            </Button>
            <Button color="error" onClick={() => setConfermaDisattiva(true)} disabled={inCorso}>
              Disattiva
            </Button>
          </Box>
        </Box>
      )}

      {confermaDisattiva && (
        <ConfirmDialog
          titolo="Disattivare la verifica in due passaggi?"
          messaggio="Da quel momento per entrare basterà di nuovo la sola password. I codici di recupero e i dispositivi ricordati verranno cancellati."
          testoConferma="Disattiva"
          inCorso={inCorso}
          onConferma={disattiva}
          onAnnulla={() => setConfermaDisattiva(false)}
        />
      )}
    </Box>
  )
}
