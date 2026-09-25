import { useEffect, useState } from 'react'
import Box from '@mui/material/Box'
import Dialog from '@mui/material/Dialog'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { apiScaricaBlob } from '../../api/client'
import { percorsoAllegato, type ContestoAssistenza, type TicketAllegatoDto } from '../../api/assistenza'
import { tokens } from '../../theme'

/**
 * Anteprima di una foto del ticket. L'immagine passa dall'Api con il token (non è un URL pubblico),
 * quindi si scarica come Blob e si mostra da un object URL, liberato quando il componente sparisce.
 */
export function FotoAllegata({ contesto, allegato }: { contesto: ContestoAssistenza; allegato: TicketAllegatoDto }) {
  const [url, setUrl] = useState<string | null>(null)
  const [errore, setErrore] = useState(false)
  const [ingrandita, setIngrandita] = useState(false)
  const percorso = allegato.eliminato ? null : percorsoAllegato(contesto, allegato.id)

  useEffect(() => {
    if (!percorso) return
    let annullato = false
    let creato: string | null = null

    apiScaricaBlob(percorso)
      .then((blob) => {
        if (annullato) return
        if (!blob) {
          setErrore(true)
          return
        }
        creato = URL.createObjectURL(blob)
        setUrl(creato)
      })
      .catch(() => {
        if (!annullato) setErrore(true)
      })

    return () => {
      annullato = true
      if (creato) URL.revokeObjectURL(creato)
    }
  }, [percorso])

  if (allegato.eliminato || errore) {
    return (
      <Box
        sx={{
          width: 96,
          height: 72,
          borderRadius: 1.5,
          border: `1px dashed ${tokens.surfaceBorder}`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          textAlign: 'center',
          px: 1,
        }}
      >
        <Typography sx={{ fontSize: 11, color: tokens.textTertiary }}>
          {allegato.eliminato ? 'Foto cancellata alla chiusura' : 'Foto non disponibile'}
        </Typography>
      </Box>
    )
  }

  if (!url) {
    return <Skeleton variant="rounded" width={96} height={72} />
  }

  return (
    <>
      <Box
        component="button"
        type="button"
        onClick={() => setIngrandita(true)}
        title={allegato.nomeFile}
        sx={{ p: 0, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, overflow: 'hidden', cursor: 'zoom-in', bgcolor: tokens.paper, lineHeight: 0 }}
      >
        <Box component="img" src={url} alt={allegato.nomeFile} sx={{ width: 96, height: 72, objectFit: 'cover' }} />
      </Box>
      <Dialog open={ingrandita} onClose={() => setIngrandita(false)} maxWidth="lg">
        <Box component="img" src={url} alt={allegato.nomeFile} onClick={() => setIngrandita(false)} sx={{ display: 'block', maxWidth: '100%', maxHeight: '85vh', cursor: 'zoom-out' }} />
      </Dialog>
    </>
  )
}
