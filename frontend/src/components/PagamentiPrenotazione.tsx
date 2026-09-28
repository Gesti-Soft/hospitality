import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/CloseOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import { ApiError } from '../api/client'
import { ETICHETTA_MODALITA_PAGAMENTO, ModalitaPagamento } from '../api/fatturazione'
import {
  ETICHETTA_TIPO_PAGAMENTO,
  TipoPagamento,
  useAggiornaPagamento,
  useEliminaPagamento,
  usePagamenti,
  useRegistraPagamento,
  type PagamentoPrenotazioneDto,
  type SalvaPagamentoRequest,
} from '../api/pagamenti'
import { formatoInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { CampoData } from './CampoData'
import { ConfirmDialog } from './ConfirmDialog'

const TIPI = Object.values(TipoPagamento) as TipoPagamento[]
const METODI = Object.values(ModalitaPagamento) as ModalitaPagamento[]
const formattatoreEuro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattaData = (iso: string) => formattatoreData.format(new Date(`${iso}T00:00:00`))

interface Props {
  strutturaId: string
  /** Null in creazione: i pagamenti restano qui e partono con la prenotazione. */
  prenotazioneId: string | null
  locali: SalvaPagamentoRequest[]
  onCambiaLocali: (righe: SalvaPagamentoRequest[]) => void
  /** Proposto come importo di un nuovo pagamento. */
  daSaldare: number
  tipoSuggerito: TipoPagamento
  puoScrivere: boolean
  disabled: boolean
}

interface Modulo {
  data: string
  importo: string
  tipo: TipoPagamento
  metodo: ModalitaPagamento | ''
  nota: string
}

/**
 * Registro dei pagamenti della prenotazione: acconto, caparra, saldo, rimborso, con data e metodo.
 * Su una prenotazione salvata ogni operazione si salva subito (come un movimento di cassa); in
 * creazione restano in attesa e partono insieme alla prenotazione.
 */
export function PagamentiPrenotazione({ strutturaId, prenotazioneId, locali, onCambiaLocali, daSaldare, tipoSuggerito, puoScrivere, disabled }: Props) {
  const mobile = useMobile()
  const toast = useToast()
  const pagamenti = usePagamenti(strutturaId, prenotazioneId)
  const registra = useRegistraPagamento(strutturaId, prenotazioneId)
  const aggiorna = useAggiornaPagamento(strutturaId, prenotazioneId)
  const elimina = useEliminaPagamento(strutturaId, prenotazioneId)
  const oggi = formatoInputData(new Date())
  const moduloVuoto = (): Modulo => ({
    data: oggi,
    importo: daSaldare > 0 ? String(daSaldare) : '',
    tipo: tipoSuggerito,
    metodo: '',
    nota: '',
  })
  const [modulo, setModulo] = useState<Modulo | null>(null)
  // Id del pagamento che si sta correggendo (null = nuovo).
  const [inCorrezione, setInCorrezione] = useState<string | null>(null)
  // Un importo registrato prima del registro non ha il metodo: correggendolo può restare senza.
  const [metodoFacoltativo, setMetodoFacoltativo] = useState(false)
  const [daEliminare, setDaEliminare] = useState<PagamentoPrenotazioneDto | null>(null)
  const inCorso = registra.isPending || aggiorna.isPending || elimina.isPending
  const bloccato = disabled || inCorso || !puoScrivere

  const righe: (SalvaPagamentoRequest & { id: string | null; registratoDa: string | null })[] = prenotazioneId
    ? (pagamenti.data ?? []).map((p) => ({ ...p }))
    : locali.map((p, i) => ({ ...p, id: `locale-${i}`, registratoDa: null }))

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function conferma() {
    if (!modulo) return
    const importo = Number(modulo.importo)
    if (!(importo > 0) || Math.round(importo * 100) !== importo * 100) {
      toast.errore("Indica un importo maggiore di zero, con al massimo due decimali.")
      return
    }
    if (modulo.metodo === '' && !metodoFacoltativo) {
      toast.errore('Indica come è stato pagato.')
      return
    }
    if (modulo.data === '' || modulo.data > oggi) {
      toast.errore('La data del pagamento non può essere nel futuro.')
      return
    }
    const richiesta: SalvaPagamentoRequest = {
      data: modulo.data,
      importo,
      tipo: modulo.tipo,
      metodo: modulo.metodo === '' ? null : modulo.metodo,
      nota: modulo.nota.trim() === '' ? null : modulo.nota.trim(),
    }

    if (!prenotazioneId) {
      onCambiaLocali(inCorrezione === null ? [...locali, richiesta] : locali.map((p, i) => (`locale-${i}` === inCorrezione ? richiesta : p)))
      chiudiModulo()
      return
    }

    const fatto = () => {
      toast.successo(inCorrezione === null ? 'Pagamento registrato.' : 'Pagamento corretto.')
      chiudiModulo()
    }
    if (inCorrezione === null) registra.mutate(richiesta, { onSuccess: fatto, onError: gestisciErrore })
    else aggiorna.mutate({ id: inCorrezione, ...richiesta }, { onSuccess: fatto, onError: gestisciErrore })
  }

  function chiudiModulo() {
    setModulo(null)
    setInCorrezione(null)
    setMetodoFacoltativo(false)
  }

  function correggi(riga: (typeof righe)[number]) {
    setInCorrezione(riga.id)
    setMetodoFacoltativo(riga.metodo === null)
    setModulo({ data: riga.data, importo: String(riga.importo), tipo: riga.tipo, metodo: riga.metodo ?? '', nota: riga.nota ?? '' })
  }

  function eseguiElimina() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, {
      onSuccess: () => {
        setDaEliminare(null)
        toast.successo('Pagamento eliminato.')
      },
      onError: (err) => {
        setDaEliminare(null)
        gestisciErrore(err)
      },
    })
  }

  if (prenotazioneId && pagamenti.isLoading) return <Skeleton variant="rounded" height={80} />

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
        Ogni incasso con la sua data e il suo metodo: in Cassa conta il giorno in cui è entrato. Se restituisci dei soldi, registra un
        rimborso. Mai i dati della carta.
      </Typography>

      {righe.length === 0 && <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>Nessun pagamento registrato.</Typography>}

      {righe.map((r) => (
        <Box
          key={r.id}
          sx={{ display: 'flex', alignItems: 'center', gap: 1, p: 1.25, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5 }}
        >
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>
              {ETICHETTA_TIPO_PAGAMENTO[r.tipo]} ·{' '}
              <Box component="span" sx={{ color: r.tipo === TipoPagamento.Rimborso ? tokens.error600 : undefined }}>
                {r.tipo === TipoPagamento.Rimborso ? '−' : ''}
                {formattatoreEuro.format(r.importo)}
              </Box>
            </Typography>
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              {formattaData(r.data)}
              {r.metodo !== null ? ` · ${ETICHETTA_MODALITA_PAGAMENTO[r.metodo]}` : ''}
              {r.registratoDa ? ` · registrato da ${r.registratoDa}` : ''}
            </Typography>
            {r.nota && <Typography sx={{ fontSize: 12, color: tokens.textSecondary, overflowWrap: 'anywhere' }}>{r.nota}</Typography>}
          </Box>
          {puoScrivere && (
            <>
              <Tooltip title="Correggi">
                <span>
                  <IconButton size="small" aria-label="Correggi pagamento" onClick={() => correggi(r)} disabled={bloccato}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
              <Tooltip title="Elimina">
                <span>
                  <IconButton
                    size="small"
                    aria-label="Elimina pagamento"
                    onClick={() =>
                      prenotazioneId
                        ? setDaEliminare(pagamenti.data?.find((p) => p.id === r.id) ?? null)
                        : onCambiaLocali(locali.filter((_, i) => `locale-${i}` !== r.id))
                    }
                    disabled={bloccato}
                  >
                    {prenotazioneId ? <DeleteIcon fontSize="small" /> : <CloseIcon fontSize="small" />}
                  </IconButton>
                </span>
              </Tooltip>
            </>
          )}
        </Box>
      ))}

      {puoScrivere && !modulo && (
        <Box>
          <Button variant="outlined" size="small" onClick={() => setModulo(moduloVuoto())} disabled={bloccato}>
            Registra pagamento
          </Button>
        </Box>
      )}

      {modulo && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, p: 1.5, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, bgcolor: tokens.surface }}>
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>
            {inCorrezione === null ? 'Nuovo pagamento' : 'Correggi pagamento'}
          </Typography>
          <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
            <Box sx={{ width: mobile ? '100%' : 170 }}>
              <CampoData label="Data" size="small" value={modulo.data} onChange={(v) => setModulo({ ...modulo, data: v })} max={oggi} fullWidth disabled={inCorso} />
            </Box>
            <TextField
              select
              size="small"
              label="Tipo"
              value={modulo.tipo}
              onChange={(e) => setModulo({ ...modulo, tipo: Number(e.target.value) as TipoPagamento })}
              disabled={inCorso}
              sx={{ width: mobile ? '100%' : 140 }}
            >
              {TIPI.map((t) => (
                <MenuItem key={t} value={t}>
                  {ETICHETTA_TIPO_PAGAMENTO[t]}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Metodo"
              value={modulo.metodo}
              onChange={(e) => setModulo({ ...modulo, metodo: e.target.value === '' ? '' : (Number(e.target.value) as ModalitaPagamento) })}
              disabled={inCorso}
              sx={{ width: mobile ? '100%' : 190 }}
            >
              {metodoFacoltativo && <MenuItem value="">Non indicato</MenuItem>}
              {METODI.map((m) => (
                <MenuItem key={m} value={m}>
                  {ETICHETTA_MODALITA_PAGAMENTO[m]}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              size="small"
              label="Importo (€)"
              type="number"
              value={modulo.importo}
              onChange={(e) => setModulo({ ...modulo, importo: e.target.value })}
              disabled={inCorso}
              slotProps={{ htmlInput: { min: 0, step: '0.01' } }}
              sx={{ width: mobile ? '100%' : 130 }}
            />
          </Box>
          <TextField
            size="small"
            label="Nota (facoltativa)"
            value={modulo.nota}
            onChange={(e) => setModulo({ ...modulo, nota: e.target.value })}
            disabled={inCorso}
            placeholder="Es. numero del bonifico"
            slotProps={{ htmlInput: { maxLength: 200 } }}
          />
          <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
            <Button size="small" onClick={chiudiModulo} disabled={inCorso}>
              Annulla
            </Button>
            <Button size="small" variant="contained" onClick={conferma} disabled={inCorso}>
              {inCorrezione === null ? 'Registra' : 'Salva correzione'}
            </Button>
          </Box>
        </Box>
      )}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare il pagamento?"
          messaggio={`${ETICHETTA_TIPO_PAGAMENTO[daEliminare.tipo]} di ${formattatoreEuro.format(daEliminare.importo)} del ${formattaData(daEliminare.data)}: resta traccia nel log. Se i soldi sono stati restituiti, registra invece un rimborso.`}
          inCorso={elimina.isPending}
          onConferma={eseguiElimina}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
