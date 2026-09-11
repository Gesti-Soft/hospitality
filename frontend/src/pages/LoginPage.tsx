import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import Link from '@mui/material/Link'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import VisibilityIcon from '@mui/icons-material/VisibilityOutlined'
import VisibilityOffIcon from '@mui/icons-material/VisibilityOffOutlined'
import { useAuth } from '../auth/AuthContext'
import { fontDisplay, tokens } from '../theme'
import { GestiSoftMark } from '../components/GestiSoftMark'

export function LoginPage() {
  const { sessione, accedi, completaVerifica2Fa, annullaVerifica2Fa, richiede2Fa, loading, errore } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [passwordVisibile, setPasswordVisibile] = useState(false)
  const [codice, setCodice] = useState('')
  const [ricordaDispositivo, setRicordaDispositivo] = useState(true)

  if (sessione) {
    return <Navigate to="/" replace />
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()

    if (richiede2Fa) {
      completaVerifica2Fa(codice, ricordaDispositivo).catch(() => {
        // L'errore è già esposto tramite `errore` dal contesto di autenticazione.
      })
      return
    }

    accedi(email, password).catch(() => {
      // Idem: il messaggio arriva dal contesto, qui basta non far esplodere la promise.
    })
  }

  function tornaAlleCredenziali() {
    annullaVerifica2Fa()
    setCodice('')
    setPassword('')
  }

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {/* Pannello brand */}
      <Box
        sx={{
          flex: '0 0 auto',
          width: { xs: 0, md: '44%' },
          display: { xs: 'none', md: 'flex' },
          flexDirection: 'column',
          justifyContent: 'space-between',
          position: 'relative',
          overflow: 'hidden',
          p: 7,
          background: `linear-gradient(165deg, ${tokens.ink900} 0%, ${tokens.ink800} 55%, #183041 100%)`,
        }}
      >
        <Box
          component="svg"
          viewBox="0 0 720 720"
          sx={{ position: 'absolute', right: -220, top: -160, width: 720, height: 720, opacity: 0.5 }}
        >
          <path d="M360 40 L620 190 V490 L360 640 L100 490 V190 Z" stroke="#2A3745" strokeWidth={1.5} fill="none" />
          <path d="M360 130 L540 235 V445 L360 550 L180 445 V235 Z" stroke="#2A3745" strokeWidth={1.5} fill="none" />
          <path d="M360 220 L460 280 V400 L360 460 L260 400 V280 Z" stroke="#2C4A5C" strokeWidth={1.5} fill="none" />
        </Box>

        <Box sx={{ position: 'relative', display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 1.75 }}>
          <GestiSoftMark size={56} />
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 26, color: '#fff' }}>GestiSoft</Typography>
        </Box>

        <Box sx={{ position: 'relative', maxWidth: 440, display: 'flex', flexDirection: 'column', gap: 2.5 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 38, lineHeight: 1.18, color: '#fff' }}>
            Il gestionale che tiene ordine nella tua struttura.
          </Typography>
          <Typography sx={{ fontSize: 15.5, lineHeight: 1.6, color: '#B8C2CE' }}>
            Niente più schedine da inviare a mano: calendario, camere, ospiti e fatturazione elettronica in un solo posto, sincronizzati con i portali OTA e con invii automatici a Polizia di Stato e Osservatorio Turistico.
          </Typography>
        </Box>

        <Box sx={{ position: 'relative', height: 22 }} />
      </Box>

      {/* Pannello form */}
      <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', p: { xs: 2.5, md: 4 } }}>
        <Box component="form" onSubmit={handleSubmit} sx={{ width: '100%', maxWidth: 380, display: 'flex', flexDirection: 'column', gap: 4 }}>
          <Box>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 27, color: tokens.ink900 }}>
              {richiede2Fa ? 'Verifica in due passaggi' : 'Accedi'}
            </Typography>
            <Typography sx={{ fontSize: 14, color: tokens.textSecondary, mt: 0.75 }}>
              {richiede2Fa
                ? 'Apri Google Authenticator sul telefono e digita il codice che vedi per GestiSoft.'
                : 'Inserisci le credenziali della tua struttura per continuare.'}
            </Typography>
          </Box>

          {errore && <Alert severity="error">{errore}</Alert>}

          {!richiede2Fa && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.25 }}>
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="nome@struttura.it"
              autoComplete="username"
              required
              fullWidth
            />
            <Box>
              <TextField
                label="Password"
                type={passwordVisibile ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
                fullWidth
                slotProps={{
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          onClick={() => setPasswordVisibile((v) => !v)}
                          edge="end"
                          size="small"
                          tabIndex={-1}
                          aria-label={passwordVisibile ? 'Nascondi password' : 'Mostra password'}
                        >
                          {passwordVisibile ? <VisibilityOffIcon fontSize="small" /> : <VisibilityIcon fontSize="small" />}
                        </IconButton>
                      </InputAdornment>
                    ),
                  },
                }}
              />
              <Box sx={{ textAlign: 'right', mt: 0.75 }}>
                <Link href="#" underline="hover" sx={{ fontSize: 12.5, fontWeight: 600 }}>
                  Password dimenticata?
                </Link>
              </Box>
            </Box>
          </Box>
          )}

          {richiede2Fa && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              <TextField
                label="Codice di verifica"
                value={codice}
                onChange={(e) => setCodice(e.target.value)}
                placeholder="000000"
                autoComplete="one-time-code"
                autoFocus
                required
                fullWidth
                helperText="Le 6 cifre dell'app Google Authenticator, oppure uno dei codici di recupero."
              />
              <FormControlLabel
                control={<Checkbox checked={ricordaDispositivo} onChange={(e) => setRicordaDispositivo(e.target.checked)} />}
                label="Non chiedermelo per 7 giorni su questo dispositivo"
                sx={{ '& .MuiFormControlLabel-label': { fontSize: 13.5 } }}
              />
            </Box>
          )}

          <Button type="submit" variant="contained" color="primary" size="large" disabled={loading} fullWidth>
            {loading ? 'Accesso in corso…' : richiede2Fa ? 'Verifica e accedi' : 'Accedi'}
          </Button>

          {richiede2Fa && (
            <Box sx={{ textAlign: 'center', mt: -2.5 }}>
              <Link component="button" type="button" onClick={tornaAlleCredenziali} underline="hover" sx={{ fontSize: 12.5, fontWeight: 600 }}>
                Torna a email e password
              </Link>
            </Box>
          )}

          <Typography sx={{ fontSize: 12, color: tokens.textTertiary, textAlign: 'center' }}>
            Problemi di accesso? Scrivi a{' '}
            <Link href="mailto:info@gestisoft.it" underline="hover">
              info@gestisoft.it
            </Link>
          </Typography>
        </Box>
      </Box>
    </Box>
  )
}
