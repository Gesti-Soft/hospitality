import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useAuth } from '../auth/AuthContext'
import { useStruttura } from '../struttura/StrutturaContext'
import { useCreaStruttura, type StrutturaDto } from '../api/strutture'
import { useDatiAziendali } from '../api/fatturazione'
import { useImpostazioni } from '../api/impostazioni'
import { ApiError } from '../api/client'
import { fontDisplay, tokens } from '../theme'
import { GestiSoftMark } from '../components/GestiSoftMark'
import { IconEsci } from '../layout/navIcons'
import { DatiAziendaliForm } from './FatturazionePage'
import { ImpostazioniGeneraliForm } from './ImpostazioniPage'

/**
 * Schermata mostrata al posto del gestionale quando un Cliente (non Super Admin) ha effettuato
 * l'accesso ma non ha ancora nessuna Struttura: prima la crea (nome), poi — nella stessa
 * schermata, riusando gli stessi form già presenti in Impostazioni/Fatturazione — completa la
 * configurazione generale e i dati aziendali prima di entrare nel gestionale vero e proprio.
 */
export function OnboardingStrutturaPage() {
  const { esci } = useAuth()
  const { selezionaStruttura } = useStruttura()
  const queryClient = useQueryClient()
  const [struttura, setStruttura] = useState<StrutturaDto | null>(null)

  function completa() {
    if (!struttura) return
    selezionaStruttura(struttura.id)
    queryClient.invalidateQueries({ queryKey: ['strutture', 'proprie'] })
  }

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: tokens.paper, display: 'flex', flexDirection: 'column' }}>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          px: 4,
          py: 2.25,
          borderBottom: `1px solid ${tokens.surfaceBorder}`,
          bgcolor: tokens.surface,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
          <GestiSoftMark size={28} />
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>GestiSoft</Typography>
        </Box>
        <Tooltip title="Esci">
          <IconButton size="small" onClick={esci}>
            <IconEsci />
          </IconButton>
        </Tooltip>
      </Box>

      <Box sx={{ flex: 1, display: 'flex', justifyContent: 'center', p: 4.5 }}>
        <Box sx={{ width: '100%', maxWidth: 720, display: 'flex', flexDirection: 'column', gap: 3 }}>
          {!struttura ? (
            <CreaStrutturaStep onCreata={setStruttura} />
          ) : (
            <CompletaConfigurazioneStep struttura={struttura} onFine={completa} />
          )}
        </Box>
      </Box>
    </Box>
  )
}

function CreaStrutturaStep({ onCreata }: { onCreata: (s: StrutturaDto) => void }) {
  const [nome, setNome] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const crea = useCreaStruttura()

  function salva() {
    if (nome.trim() === '') {
      setErrore('Indica il nome della struttura.')
      return
    }
    setErrore(null)
    crea.mutate(
      { nome: nome.trim() },
      {
        onSuccess: onCreata,
        onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Box>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 24 }}>Benvenuto su GestiSoft</Typography>
      <Typography sx={{ fontSize: 14, color: tokens.textSecondary, mt: 0.75, mb: 3 }}>
        Non hai ancora nessuna struttura configurata. Dalle un nome per iniziare — potrai crearne altre in seguito.
      </Typography>

      <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 480 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <TextField
          label="Nome struttura"
          placeholder="es. Villa Chifeci Scopello"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          fullWidth
          disabled={crea.isPending}
          autoFocus
          onKeyDown={(e) => e.key === 'Enter' && salva()}
        />
        <Box>
          <Button variant="contained" color="primary" onClick={salva} disabled={crea.isPending}>
            {crea.isPending ? 'Creazione…' : 'Crea struttura'}
          </Button>
        </Box>
      </Box>
    </Box>
  )
}

function CompletaConfigurazioneStep({ struttura, onFine }: { struttura: StrutturaDto; onFine: () => void }) {
  const impostazioni = useImpostazioni(struttura.id)
  const datiAziendali = useDatiAziendali(struttura.id)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 800, fontSize: 24 }}>{struttura.nome} creata</Typography>
        <Typography sx={{ fontSize: 14, color: tokens.textSecondary, mt: 0.75 }}>
          Completa ora la configurazione generale e i dati aziendali — ti serviranno per la tassa di soggiorno e per le
          fatture. Puoi anche saltare e completarli in seguito da Impostazioni.
        </Typography>
      </Box>

      {impostazioni.isLoading && <Skeleton variant="rounded" height={260} />}
      {!impostazioni.isLoading && impostazioni.data && (
        <ImpostazioniGeneraliForm strutturaId={struttura.id} dati={impostazioni.data} servizi={struttura} />
      )}

      {datiAziendali.isLoading && <Skeleton variant="rounded" height={320} />}
      {!datiAziendali.isLoading && datiAziendali.data && <DatiAziendaliForm strutturaId={struttura.id} dati={datiAziendali.data} />}

      <Box>
        <Button variant="contained" color="primary" size="large" onClick={onFine}>
          Vai al gestionale
        </Button>
      </Box>
    </Box>
  )
}
