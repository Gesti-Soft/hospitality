import { useRef } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'
import { ALLEGATI_MAX_PER_MESSAGGIO, ALLEGATO_MAX_BYTE, TIPI_FOTO_ACCETTATI } from '../../api/assistenza'
import { tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'

const MB = 1024 * 1024

/** Pulsante "Allega foto" con l'elenco dei file scelti. I limiti li ricontrolla l'Api: qui si evita solo di caricare per niente. */
export function SelettoreFoto({ foto, onChange, disabilitato }: { foto: File[]; onChange: (foto: File[]) => void; disabilitato?: boolean }) {
  const input = useRef<HTMLInputElement | null>(null)
  const toast = useToast()

  function aggiungi(scelti: FileList | null) {
    if (!scelti) return
    const nuovi = [...foto]
    for (const file of Array.from(scelti)) {
      if (!TIPI_FOTO_ACCETTATI.split(',').includes(file.type)) {
        toast.errore(`${file.name} non è una foto: si possono allegare solo immagini PNG, JPEG o WebP.`)
        continue
      }
      if (file.size > ALLEGATO_MAX_BYTE) {
        toast.errore(`${file.name} supera il limite di ${ALLEGATO_MAX_BYTE / MB} MB.`)
        continue
      }
      if (nuovi.length >= ALLEGATI_MAX_PER_MESSAGGIO) {
        toast.errore(`Puoi allegare al massimo ${ALLEGATI_MAX_PER_MESSAGGIO} foto per messaggio.`)
        break
      }
      nuovi.push(file)
    }
    onChange(nuovi)
    // Svuotato, così si può riscegliere lo stesso file dopo averlo tolto.
    if (input.current) input.current.value = ''
  }

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1 }}>
      <input ref={input} type="file" accept={TIPI_FOTO_ACCETTATI} multiple hidden onChange={(e) => aggiungi(e.target.files)} />
      <Button
        size="small"
        variant="outlined"
        onClick={() => input.current?.click()}
        disabled={disabilitato || foto.length >= ALLEGATI_MAX_PER_MESSAGGIO}
      >
        Allega foto
      </Button>
      {foto.map((f, i) => (
        <Chip
          key={`${f.name}-${i}`}
          size="small"
          label={`${f.name} · ${(f.size / MB).toFixed(1)} MB`}
          onDelete={disabilitato ? undefined : () => onChange(foto.filter((_, j) => j !== i))}
          sx={{ maxWidth: 260 }}
        />
      ))}
      {foto.length === 0 && (
        <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary }}>
          Fino a {ALLEGATI_MAX_PER_MESSAGGIO} foto da {ALLEGATO_MAX_BYTE / MB} MB. Si cancellano quando il ticket viene chiuso.
        </Typography>
      )}
    </Box>
  )
}
