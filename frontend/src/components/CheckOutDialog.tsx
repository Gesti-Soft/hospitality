import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { ETICHETTA_MODALITA_PAGAMENTO, ModalitaPagamento, nomeDocumento, testoDaFatturare, useDaFatturare, useFatturaPerPrenotazione } from '../api/fatturazione'
import { TipoPagamento, nettoPagamenti, usePagamenti, useRegistraPagamento } from '../api/pagamenti'
import { useCheckOut, type PrenotazioneDto } from '../api/prenotazioni'
import { formatoInputData } from '../lib/date'
import { usePuoScrivere } from '../permessi/usePuoScrivere'
import { tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { ProponiFatturaDopoCheckIn } from './ProponiFatturaDopoCheckIn'

const METODI = Object.values(ModalitaPagamento) as ModalitaPagamento[]
const formattatoreEuro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

/**
 * Check-out con il conto davanti: totale, pagato e quanto resta da saldare, con il saldo che si
 * registra qui (metodo e importo). Si può chiudere anche con un saldo aperto (un bonifico in arrivo),
 * ma dopo averlo visto. Poi la cauzione, come prima, e se il soggiorno non è ancora fatturato la
 * proposta di emettere il documento. Unico per la pagina Check-in/out e per il dettaglio prenotazione.
 */
export function CheckOutDialog({
  strutturaId,
  prenotazione,
  cauzione,
  onChiudi,
  onCompletato,
}: {
  strutturaId: string
  prenotazione: PrenotazioneDto
  /** Importo della cauzione della tipologia se la prenotazione ha il toggle attivo, altrimenti 0. */
  cauzione: number
  onChiudi: () => void
  /** Dopo il check-out (e l'eventuale fattura): chi l'ha aperto può chiudere anche il resto. */
  onCompletato: () => void
}) {
  const toast = useToast()
  const puoRegistrarePagamenti = usePuoScrivere('reservationWrite')
  const puoFatturare = usePuoScrivere('financeWrite')
  const pagamenti = usePagamenti(strutturaId, prenotazione.id)
  const registra = useRegistraPagamento(strutturaId, prenotazione.id)
  const checkOut = useCheckOut(strutturaId)
  const fattura = useFatturaPerPrenotazione(puoFatturare ? strutturaId : null, prenotazione.id)
  const extraDaFatturare = testoDaFatturare(useDaFatturare(fattura.data ? strutturaId : null, prenotazione.id).data)
  const [restituisciCauzione, setRestituisciCauzione] = useState(true)
  const [importoCauzioneTrattenuta, setImportoCauzioneTrattenuta] = useState('')
  const [metodoSaldo, setMetodoSaldo] = useState<ModalitaPagamento | ''>('')
  const [importoSaldo, setImportoSaldo] = useState<string | null>(null)
  const [proponiFattura, setProponiFattura] = useState<PrenotazioneDto | null>(null)

  const totale = prenotazione.importoTotale ?? 0
  const pagato = pagamenti.data ? nettoPagamenti(pagamenti.data) : (prenotazione.importoPagato ?? 0)
  const daSaldare = Math.round((totale - pagato) * 100) / 100
  const inCorso = registra.isPending || checkOut.isPending
  const cauzionePrevista = cauzione > 0

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function registraSaldo() {
    const importo = Number(importoSaldo ?? daSaldare)
    if (!(importo > 0)) {
      toast.errore("Indica l'importo incassato.")
      return
    }
    if (metodoSaldo === '') {
      toast.errore('Indica come ha pagato.')
      return
    }
    registra.mutate(
      { data: formatoInputData(new Date()), importo, tipo: TipoPagamento.Saldo, metodo: metodoSaldo, nota: null },
      {
        onSuccess: () => {
          toast.successo('Saldo registrato.')
          setImportoSaldo(null)
        },
        onError: gestisciErrore,
      },
    )
  }

  function eseguiCheckOut() {
    const trattenuta = restituisciCauzione || importoCauzioneTrattenuta.trim() === '' ? null : Number(importoCauzioneTrattenuta)
    checkOut.mutate(
      { prenotazioneId: prenotazione.id, restituisciCauzione: !cauzionePrevista || restituisciCauzione, importoCauzioneTrattenuta: cauzionePrevista ? trattenuta : null },
      {
        onSuccess: (aggiornata) => {
          toast.successo(`Check-out effettuato${prenotazione.numeroPrenotazione ? ` — #${prenotazione.numeroPrenotazione}` : ''}.`)
          // Il documento si propone se non c'è ancora, o se ci sono extra addebitati dopo quello fatto
          // al check-in: è il momento del saldo, dopo è facile dimenticarli.
          if (puoFatturare && (fattura.data === null || extraDaFatturare)) {
            setProponiFattura(aggiornata)
            return
          }
          onCompletato()
        },
        onError: gestisciErrore,
      },
    )
  }

  if (proponiFattura) {
    return (
      <ProponiFatturaDopoCheckIn
        strutturaId={strutturaId}
        prenotazione={proponiFattura}
        evento="check-out"
        extra={fattura.data && extraDaFatturare ? { documento: nomeDocumento(fattura.data).toLowerCase(), daFatturare: extraDaFatturare } : undefined}
        onChiudi={onCompletato}
      />
    )
  }

  return (
    <Dialog open onClose={inCorso ? undefined : onChiudi} maxWidth="xs" fullWidth>
      <DialogTitle>Check-out{prenotazione.numeroPrenotazione ? ` — #${prenotazione.numeroPrenotazione}` : ''}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
        {pagamenti.isLoading ? (
          <Skeleton variant="rounded" height={90} />
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
            <RigaConto etichetta="Totale del soggiorno" valore={formattatoreEuro.format(totale)} />
            <RigaConto etichetta="Pagato" valore={formattatoreEuro.format(pagato)} />
            <Divider sx={{ my: 0.5 }} />
            <RigaConto
              etichetta={daSaldare >= 0 ? 'Da saldare' : 'Pagato in più'}
              valore={formattatoreEuro.format(Math.abs(daSaldare))}
              evidenza={daSaldare > 0}
            />
          </Box>
        )}

        {daSaldare > 0 && puoRegistrarePagamenti && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, p: 1.5, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5 }}>
            <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>Incassa il saldo</Typography>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
              <TextField
                select
                size="small"
                label="Metodo"
                value={metodoSaldo}
                onChange={(e) => setMetodoSaldo(e.target.value === '' ? '' : (Number(e.target.value) as ModalitaPagamento))}
                disabled={inCorso}
                sx={{ flex: 1, minWidth: 150 }}
              >
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
                value={importoSaldo ?? String(daSaldare)}
                onChange={(e) => setImportoSaldo(e.target.value)}
                disabled={inCorso}
                slotProps={{ htmlInput: { min: 0, step: '0.01' } }}
                sx={{ width: 120 }}
              />
            </Box>
            <Box>
              <Button size="small" variant="outlined" onClick={registraSaldo} disabled={inCorso}>
                Registra saldo
              </Button>
            </Box>
          </Box>
        )}

        {cauzionePrevista && (
          <>
            <FormControlLabel
              control={<Checkbox checked={restituisciCauzione} onChange={(e) => setRestituisciCauzione(e.target.checked)} disabled={inCorso} />}
              label={`Restituisci l'intera cauzione al cliente (${formattatoreEuro.format(cauzione)})`}
            />
            {!restituisciCauzione && (
              <TextField
                label="Importo cauzione trattenuta (€)"
                type="number"
                size="small"
                value={importoCauzioneTrattenuta}
                onChange={(e) => setImportoCauzioneTrattenuta(e.target.value)}
                disabled={inCorso}
                helperText={`Cauzione versata: ${formattatoreEuro.format(cauzione)}`}
              />
            )}
          </>
        )}

        {daSaldare > 0 && !pagamenti.isLoading && (
          <Alert severity="warning">
            Restano da saldare {formattatoreEuro.format(daSaldare)}. Puoi fare il check-out lo stesso (per esempio un bonifico in arrivo): il
            conto resta aperto e il saldo si registra più tardi dalla prenotazione.
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onChiudi} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={eseguiCheckOut} disabled={inCorso || pagamenti.isLoading}>
          {daSaldare > 0 ? 'Check-out con saldo aperto' : 'Conferma check-out'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function RigaConto({ etichetta, valore, evidenza = false }: { etichetta: string; valore: string; evidenza?: boolean }) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}>
      <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>{etichetta}</Typography>
      <Typography sx={{ fontSize: 13.5, fontWeight: evidenza ? 700 : 600, color: evidenza ? tokens.orange600 : undefined }}>{valore}</Typography>
    </Box>
  )
}
