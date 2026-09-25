import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import { TipoVariazionePrezzo } from '../api/tipologie'

/** Scelta tra euro e percentuale: la stessa per supplementi, riduzioni, fasce d'età e trattamenti. */
export function SceltaEuroPercentuale({
  valore,
  onChange,
  disabled,
}: {
  valore: TipoVariazionePrezzo
  onChange: (v: TipoVariazionePrezzo) => void
  disabled: boolean
}) {
  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      value={valore}
      onChange={(_, v: TipoVariazionePrezzo | null) => {
        if (v) onChange(v)
      }}
      disabled={disabled}
    >
      <ToggleButton value={TipoVariazionePrezzo.Euro} aria-label="In euro">
        €
      </ToggleButton>
      <ToggleButton value={TipoVariazionePrezzo.Percentuale} aria-label="In percentuale">
        %
      </ToggleButton>
    </ToggleButtonGroup>
  )
}
