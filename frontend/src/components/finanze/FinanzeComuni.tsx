import { forwardRef } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import SearchIcon from '@mui/icons-material/Search'
import { fontMono, tokens } from '../../theme'
import { CampoData } from '../CampoData'
import { BottoneNuovo } from '../CardElenco'
import { inizioGiornoLocale, parsaInputData } from '../../lib/date'
import type { CameraDto } from '../../api/camere'
import type { TipologiaCameraDto } from '../../api/tipologie'

export { ANNO_CORRENTE } from '../../lib/anni'

export const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
export const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

/**
 * Intestazione comune alle 4 pagine di Finanze: selettore Anno (ed eventuali azioni) a destra.
 * `anni` è a carico di chi chiama (vedi `anniConAnnoCorrente`) — su richiesta esplicita propone
 * solo anni con dati reali più l'anno corrente, mai un range fisso arbitrario.
 */
export function IntestazioneFinanze({
  anno,
  anni,
  onAnnoChange,
  azioni,
}: {
  anno: number
  anni: number[]
  onAnnoChange: (anno: number) => void
  azioni?: React.ReactNode
}) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {azioni}
        <TextField select size="small" label="Anno" value={anno} onChange={(e) => onAnnoChange(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {anni.map((a) => (
            <MenuItem key={a} value={a}>
              {a}
            </MenuItem>
          ))}
        </TextField>
      </Box>
    </Box>
  )
}

export function AzioneNuovo({ etichetta, onClick, disabilitato }: { etichetta: string; onClick: () => void; disabilitato?: boolean }) {
  return <BottoneNuovo etichetta={etichetta} onClick={onClick} disabilitato={disabilitato} />
}

export function Cornice({ children }: { children: React.ReactNode }) {
  return <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>{children}</Box>
}

/**
 * Riga di filtri comune alle pagine di Finanze: casella di ricerca testuale sempre presente, più un
 * intervallo di date "Da"/"A" opzionale (solo dove ha senso — es. non sulla rubrica Clienti
 * fatturabili, che non ha una data propria). Il filtro vero e proprio resta a carico di ogni pagina
 * (campi di ricerca ed eventuale campo data diversi caso per caso): qui c'è solo la UI condivisa.
 */
export function FiltriRicercaData({
  ricerca,
  onRicercaChange,
  placeholderRicerca,
  dataDa,
  onDataDaChange,
  dataA,
  onDataAChange,
}: {
  ricerca: string
  onRicercaChange: (valore: string) => void
  placeholderRicerca: string
  dataDa?: string
  onDataDaChange?: (valore: string) => void
  dataA?: string
  onDataAChange?: (valore: string) => void
}) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      <TextField
        size="small"
        placeholder={placeholderRicerca}
        value={ricerca}
        onChange={(e) => onRicercaChange(e.target.value)}
        sx={{ minWidth: 260, flex: '1 1 260px' }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: tokens.textTertiary }} />
              </InputAdornment>
            ),
          },
        }}
      />
      {onDataDaChange && onDataAChange && (
        <>
          <CampoData label="Da" value={dataDa ?? ''} onChange={onDataDaChange} size="small" max={dataA || undefined} />
          <CampoData label="A" value={dataA ?? ''} onChange={onDataAChange} size="small" min={dataDa || undefined} />
        </>
      )}
    </Box>
  )
}

/**
 * True se `valore` (data ISO del record, es. dataSpesa) cade nell'intervallo "Da"/"A" (formato
 * "YYYY-MM-DD" di CampoData, entrambi opzionali). Un record senza data propria non corrisponde a
 * nessun intervallo impostato — non ha senso includerlo "per difetto" in un filtro per data.
 */
export function nelRangeData(valore: string | null | undefined, da: string, a: string): boolean {
  if (!da && !a) return true
  if (!valore) return false

  const giorno = inizioGiornoLocale(new Date(valore))
  if (da && giorno < parsaInputData(da)) return false
  if (a && giorno > parsaInputData(a)) return false
  return true
}

export function RigaVuota({ colSpan, messaggio }: { colSpan: number; messaggio: string }) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
        {messaggio}
      </TableCell>
    </TableRow>
  )
}

/**
 * Barra del totale sotto la tabella, fuori dalla `Cornice` e con `position: sticky` sul fondo
 * dell'area di contenuto scrollabile (vedi `overflow: 'auto'` in AppShell): con la paginazione a
 * scroll infinito la tabella può crescere molto in altezza, e un totale dentro l'ultima riga della
 * tabella sarebbe visibile solo scrollando fino in fondo a tutto l'elenco — qui invece resta sempre
 * a vista, indipendentemente da quante righe sono già state caricate.
 */
export function BarraTotale({ etichetta, valore, colore }: { etichetta: string; valore: number; colore?: string }) {
  return (
    <Box
      sx={{
        position: 'sticky',
        bottom: 0,
        zIndex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: 2,
        py: 1.25,
        bgcolor: tokens.surface,
        border: `1px solid ${tokens.surfaceBorder}`,
        borderRadius: 2,
        boxShadow: '0 -4px 10px rgba(0,0,0,0.06)',
      }}
    >
      <Typography sx={{ fontSize: 13, fontWeight: 700, color: tokens.textSecondary }}>{etichetta}</Typography>
      <Typography sx={{ fontFamily: fontMono, fontWeight: 700, fontSize: 15, color: colore ?? tokens.textPrimary }}>
        {formattatoreValuta.format(valore)}
      </Typography>
    </Box>
  )
}

const GENERALI = 'Generali'
const SEPARATORE_CAMERA = ' - '

/**
 * "Tipo spesa"/"Tipo entrata" restano campi di testo libero lato backend (porta 1:1 dal sistema
 * legacy) — qui si riconosce solo se il testo corrente combacia con "Generali" oppure con una
 * Tipologia (eventualmente seguita da " - Nome camera") per mostrare le select a cascata del
 * vecchio programma. Un valore che non combacia (es. testo libero già in uso, tipologia rinominata
 * o eliminata) resta comunque modificabile come testo libero, senza perderlo.
 */
function trovaTipologiaECamera(testo: string, tipologie: TipologiaCameraDto[], camere: CameraDto[]) {
  const valore = testo.trim()
  if (!valore) return null

  const indiceSeparatore = valore.lastIndexOf(SEPARATORE_CAMERA)
  if (indiceSeparatore > 0) {
    const nomeTipologia = valore.slice(0, indiceSeparatore)
    const nomeCamera = valore.slice(indiceSeparatore + SEPARATORE_CAMERA.length)
    const tipologia = tipologie.find((t) => t.tipologiaCamera.toLowerCase() === nomeTipologia.toLowerCase())
    if (tipologia) {
      const camera = camere.find((c) => c.tipologiaId === tipologia.id && c.nome.toLowerCase() === nomeCamera.toLowerCase()) ?? null
      return { tipologia, camera }
    }
  }

  const tipologia = tipologie.find((t) => t.tipologiaCamera.toLowerCase() === valore.toLowerCase())
  return tipologia ? { tipologia, camera: null } : null
}

/**
 * Select "Generali oppure Tipologia" con select a cascata "Camera" quando si sceglie una
 * Tipologia — stesso comportamento del vecchio programma, riusato da Spese ed Entrate (unici due
 * moduli Finanze con un campo "Tipo …" testuale). Il valore risultante resta una singola stringa
 * (vedi `trovaTipologiaECamera`): "Generali", il nome della tipologia, o "Tipologia - Camera".
 */
export function SelettoreTipoConCamera({
  label,
  valore,
  onChange,
  tipologie,
  camere,
  disabled,
}: {
  label: string
  valore: string
  onChange: (valore: string) => void
  tipologie: TipologiaCameraDto[]
  camere: CameraDto[]
  disabled?: boolean
}) {
  const opzioni = [GENERALI, ...tipologie.map((t) => t.tipologiaCamera)]
  const corrispondenza = trovaTipologiaECamera(valore, tipologie, camere)
  const camereTipologia = corrispondenza ? camere.filter((c) => c.tipologiaId === corrispondenza.tipologia.id) : []

  return (
    <>
      <Autocomplete
        freeSolo
        forcePopupIcon
        fullWidth
        disabled={disabled}
        options={opzioni}
        inputValue={valore}
        onInputChange={(_, nuovoValore) => onChange(nuovoValore)}
        renderInput={(params) => <TextField {...params} label={label} />}
      />
      {corrispondenza && (
        <Autocomplete
          fullWidth
          disabled={disabled}
          options={camereTipologia}
          getOptionLabel={(c) => c.nome}
          isOptionEqualToValue={(a, b) => a.id === b.id}
          value={corrispondenza.camera}
          onChange={(_, nuovaCamera) =>
            onChange(nuovaCamera ? `${corrispondenza.tipologia.tipologiaCamera}${SEPARATORE_CAMERA}${nuovaCamera.nome}` : corrispondenza.tipologia.tipologiaCamera)
          }
          renderInput={(params) => <TextField {...params} label="Camera" />}
        />
      )}
    </>
  )
}

/** Riga "sentinella" per la paginazione a scroll infinito (vedi usePaginazioneScroll) — il ref va sull'elemento che l'IntersectionObserver osserva. */
export const RigaCaricamentoAltri = forwardRef<HTMLTableRowElement, { colSpan: number }>(function RigaCaricamentoAltri({ colSpan }, ref) {
  return (
    <TableRow ref={ref}>
      <TableCell colSpan={colSpan} sx={{ textAlign: 'center', color: tokens.textTertiary, py: 2, fontSize: 12 }}>
        Caricamento altri...
      </TableCell>
    </TableRow>
  )
})
