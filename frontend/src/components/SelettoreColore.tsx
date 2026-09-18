import { useState, type MouseEvent, type PointerEvent } from 'react'
import Box from '@mui/material/Box'
import Popover from '@mui/material/Popover'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { hexToHsv, hsvToHex, hsvToRgb, normalizzaHex, rgbToHex, rgbToHsv, type Hsv, type Rgb } from '../lib/colore'
import { tokens } from '../theme'

interface Props {
  /** Colore corrente in esadecimale. Un valore non valido viene mostrato come nero, senza rompere il selettore. */
  value: string
  onChange: (hex: string) => void
  disabled?: boolean
  /** Etichetta del pulsante per i lettori di schermo — il quadratino di colore da solo non dice niente. */
  ariaLabel?: string
}

const DIMENSIONE_PASTIGLIA = 14

/**
 * Selettore di colore completo: quadrato saturazione × luminosità, barra delle tonalità e i valori
 * scrivibili sia in esadecimale sia nelle tre componenti RGB. Sostituisce `<input type="color">`,
 * che delegava la scelta alla finestra di sistema — diversa su ogni computer, in nessun modo
 * accordata al resto dell'interfaccia, e senza un campo dove incollare un esadecimale, che è il
 * modo in cui un colore arriva quasi sempre (dal sito del portale, da un logo, da un grafico).
 *
 * La tonalità è tenuta a parte in HSV invece che ricavata ogni volta dall'esadecimale: su un nero o
 * su un grigio la tonalità non esiste più, e ricalcolandola il cursore salterebbe sul rosso appena
 * si porta la luminosità a zero.
 */
export function SelettoreColore({ value, onChange, disabled = false, ariaLabel = 'Scegli un colore' }: Props) {
  const [ancora, setAncora] = useState<HTMLElement | null>(null)
  const [hsv, setHsv] = useState<Hsv>(() => hexToHsv(value))
  const [testoHex, setTestoHex] = useState(() => normalizzaHex(value) ?? '#000000')
  // Ultimo colore che questo componente ha mostrato: serve a distinguere un cambiamento arrivato da
  // fuori (una pastiglia della palette, il caricamento di un canale esistente) da uno prodotto qui
  // dentro, che ha già aggiornato tutto e non va rincorso — rileggendolo si perderebbero la
  // tonalità e il testo digitato a metà.
  const [ultimoValore, setUltimoValore] = useState(value)

  if (value !== ultimoValore) {
    setUltimoValore(value)
    setHsv(hexToHsv(value))
    setTestoHex(normalizzaHex(value) ?? '#000000')
  }

  const hex = hsvToHex(hsv)
  const rgb = hsvToRgb(hsv)

  function applica(hsvNuovo: Hsv, testo?: string) {
    const hexNuovo = hsvToHex(hsvNuovo)
    setHsv(hsvNuovo)
    setTestoHex(testo ?? hexNuovo)
    setUltimoValore(hexNuovo)
    onChange(hexNuovo)
  }

  /**
   * La tonalità di partenza va tenuta quando il colore scritto è un grigio (saturazione 0) o il
   * nero: lì la conversione la azzera, e il cursore del quadrato tornerebbe sul rosso da solo
   * mentre si sta ancora scrivendo.
   */
  function conservandoTonalita(convertito: Hsv): Hsv {
    return { ...convertito, h: convertito.s === 0 || convertito.v === 0 ? hsv.h : convertito.h }
  }

  function applicaRgb(rgbNuovo: Rgb) {
    applica(conservandoTonalita(rgbToHsv(rgbNuovo)), rgbToHex(rgbNuovo))
  }

  function scriviHex(testo: string) {
    setTestoHex(testo)
    const normalizzato = normalizzaHex(testo)
    if (normalizzato === null) {
      // Testo incompleto mentre si digita: si lascia scrivere senza toccare il colore, che resta
      // l'ultimo valido — cambiarlo a ogni carattere farebbe lampeggiare tutta la finestra.
      return
    }
    // Si passa il testo così com'è stato digitato: normalizzarlo adesso sposterebbe il cursore
    // sotto le dita. Ci pensa l'uscita dal campo (onBlur) a riscriverlo in forma canonica.
    applica(conservandoTonalita(hexToHsv(normalizzato)), testo)
  }

  function posizioneRelativa(e: PointerEvent<HTMLDivElement>) {
    const area = e.currentTarget.getBoundingClientRect()
    return {
      x: Math.min(Math.max((e.clientX - area.left) / area.width, 0), 1),
      y: Math.min(Math.max((e.clientY - area.top) / area.height, 0), 1),
    }
  }

  function trascina(e: PointerEvent<HTMLDivElement>, aggiorna: (e: PointerEvent<HTMLDivElement>) => void) {
    // Il puntatore viene catturato dall'area: il trascinamento continua anche uscendo dal riquadro,
    // come in qualunque altro selettore, invece di fermarsi sul bordo.
    e.currentTarget.setPointerCapture(e.pointerId)
    aggiorna(e)
  }

  function muoviQuadrato(e: PointerEvent<HTMLDivElement>) {
    const { x, y } = posizioneRelativa(e)
    applica({ ...hsv, s: x * 100, v: (1 - y) * 100 })
  }

  function muoviTonalita(e: PointerEvent<HTMLDivElement>) {
    applica({ ...hsv, h: posizioneRelativa(e).x * 360 })
  }

  const campoComponente = (etichetta: string, valore: number, scrivi: (n: number) => void) => (
    <TextField
      label={etichetta}
      type="number"
      size="small"
      value={valore}
      onChange={(e) => scrivi(Math.min(Math.max(Math.round(Number(e.target.value) || 0), 0), 255))}
      slotProps={{ htmlInput: { min: 0, max: 255 } }}
      sx={{ flex: 1 }}
    />
  )

  return (
    <>
      {/* Lo span attorno al pulsante serve al Tooltip quando il pulsante è disabilitato: un elemento disabilitato non emette eventi del mouse, e il messaggio non comparirebbe. */}
      <Tooltip title={ariaLabel}>
        <Box component="span" sx={{ display: 'inline-flex' }}>
          <Box
            component="button"
            type="button"
            aria-label={ariaLabel}
            disabled={disabled}
            onClick={(e: MouseEvent<HTMLButtonElement>) => setAncora(e.currentTarget)}
            sx={{
              width: 34,
              height: 26,
              p: 0,
              border: `1px solid ${tokens.surfaceBorder}`,
              borderRadius: 1,
              bgcolor: normalizzaHex(value) ?? '#000000',
              cursor: disabled ? 'default' : 'pointer',
              opacity: disabled ? 0.5 : 1,
            }}
          />
        </Box>
      </Tooltip>

      <Popover
        open={ancora !== null}
        anchorEl={ancora}
        onClose={() => setAncora(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
        slotProps={{ paper: { sx: { mt: 1, borderRadius: 2 } } }}
      >
        <Box sx={{ p: 2, width: 264, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <Box
            onPointerDown={(e) => trascina(e, muoviQuadrato)}
            onPointerMove={(e) => e.currentTarget.hasPointerCapture(e.pointerId) && muoviQuadrato(e)}
            sx={{
              position: 'relative',
              height: 150,
              borderRadius: 1.5,
              cursor: 'crosshair',
              touchAction: 'none',
              background: `linear-gradient(to top, #000, rgba(0,0,0,0)), linear-gradient(to right, #FFF, hsl(${hsv.h} 100% 50%))`,
            }}
          >
            <Pastiglia sinistra={`${hsv.s}%`} alto={`${100 - hsv.v}%`} colore={hex} />
          </Box>

          <Box
            onPointerDown={(e) => trascina(e, muoviTonalita)}
            onPointerMove={(e) => e.currentTarget.hasPointerCapture(e.pointerId) && muoviTonalita(e)}
            sx={{
              position: 'relative',
              height: 14,
              borderRadius: 7,
              cursor: 'pointer',
              touchAction: 'none',
              background: 'linear-gradient(to right, #F00 0%, #FF0 17%, #0F0 33%, #0FF 50%, #00F 67%, #F0F 83%, #F00 100%)',
            }}
          >
            <Pastiglia sinistra={`${(hsv.h / 360) * 100}%`} alto="50%" colore={`hsl(${hsv.h} 100% 50%)`} />
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <Box sx={{ width: 42, height: 42, borderRadius: 1.5, bgcolor: hex, border: `1px solid ${tokens.surfaceBorder}`, flex: '0 0 auto' }} />
            <TextField
              label="Esadecimale"
              size="small"
              value={testoHex}
              onChange={(e) => scriviHex(e.target.value)}
              onBlur={() => setTestoHex(hex)}
              slotProps={{ htmlInput: { maxLength: 7, spellCheck: false } }}
              sx={{ flex: 1 }}
            />
          </Box>

          <Box sx={{ display: 'flex', gap: 1 }}>
            {campoComponente('Rosso', rgb.r, (r) => applicaRgb({ ...rgb, r }))}
            {campoComponente('Verde', rgb.g, (g) => applicaRgb({ ...rgb, g }))}
            {campoComponente('Blu', rgb.b, (b) => applicaRgb({ ...rgb, b }))}
          </Box>

          <Typography sx={{ fontSize: 11.5, color: tokens.textSecondary }}>Puoi anche incollare un esadecimale, con o senza #.</Typography>
        </Box>
      </Popover>
    </>
  )
}

/** Cursore tondo sopra il quadrato o la barra: bordo bianco e ombra sottile, per restare visibile su qualunque colore ci finisca sotto. */
function Pastiglia({ sinistra, alto, colore }: { sinistra: string; alto: string; colore: string }) {
  return (
    <Box
      sx={{
        position: 'absolute',
        left: sinistra,
        top: alto,
        width: DIMENSIONE_PASTIGLIA,
        height: DIMENSIONE_PASTIGLIA,
        ml: `${-DIMENSIONE_PASTIGLIA / 2}px`,
        mt: `${-DIMENSIONE_PASTIGLIA / 2}px`,
        borderRadius: '50%',
        bgcolor: colore,
        border: '2px solid #FFF',
        boxShadow: '0 0 0 1px rgba(0,0,0,0.35)',
        pointerEvents: 'none',
      }}
    />
  )
}
