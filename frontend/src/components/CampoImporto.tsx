import TextField, { type TextFieldProps } from '@mui/material/TextField'

type Props = Omit<TextFieldProps, 'type' | 'value' | 'onChange'> & {
  /** Il numero col punto, come lo leggono Number() e il backend; '' = vuoto. */
  value: string
  /** Riceve il numero col punto (es. "1045.84"), o '' se il campo è stato svuotato. */
  onChange: (valore: string) => void
  /** Ammette il segno meno (una riga di sconto in fattura). */
  ammettiNegativo?: boolean
}

/** Decimali ammessi digitando: i centesimi. Un valore che ne ha già di più resta modificabile. */
const DECIMALI = 2

/**
 * Campo per un importo scritto all'italiana: si vede e si digita con la virgola, e il punto (del
 * tastierino numerico, o per abitudine) diventa subito una virgola. Un campo `type="number"` non lo
 * permette: il browser non lascia toccare il testo mentre lo si digita. Al resto del codice arriva
 * sempre il numero col punto, quindi calcoli e salvataggi non cambiano.
 *
 * Incollando "1.234,56" (da Excel o dalla banca) i punti sono separatori delle migliaia e si tolgono.
 */
export function CampoImporto({ value, onChange, ammettiNegativo = false, slotProps, ...resto }: Props) {
  const visibile = value.replace('.', ',')

  return (
    <TextField
      {...resto}
      value={visibile}
      onChange={(e) => {
        const normalizzato = normalizzaImporto(e.target.value, ammettiNegativo, Math.max(DECIMALI, decimali(visibile)))
        // Un carattere non valido (una lettera, una seconda virgola, un terzo decimale) si ignora: il campo resta com'era.
        if (normalizzato !== null) onChange(normalizzato)
      }}
      slotProps={{ ...slotProps, htmlInput: { ...(slotProps?.htmlInput as object | undefined), inputMode: 'decimal' } }}
    />
  )
}

function decimali(testo: string): number {
  const virgola = testo.indexOf(',')
  return virgola < 0 ? 0 : testo.length - virgola - 1
}

/** Il testo digitato col punto decimale ("1045.84"), oppure null se non è un importo valido. */
function normalizzaImporto(testo: string, ammettiNegativo: boolean, maxDecimali: number): string | null {
  let t = testo.replace(/[\s€]/g, '')
  if (t.includes(',') || (t.match(/\./g) ?? []).length > 1) {
    // "1.234,56" o "1.234.567": i punti separano le migliaia.
    t = t.replace(/\./g, '')
  } else {
    t = t.replace('.', ',')
  }

  const formato = new RegExp(`^${ammettiNegativo ? '-?' : ''}\\d*(,\\d{0,${maxDecimali}})?$`)
  return formato.test(t) ? t.replace(',', '.') : null
}
