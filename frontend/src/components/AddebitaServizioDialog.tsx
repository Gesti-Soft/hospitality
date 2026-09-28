import { useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import type { PrenotazioneDto } from '../api/prenotazioni'
import {
  NOME_MODALITA,
  importoRiga,
  nottiTra,
  perNotte,
  perPersona,
  useAddebitaServizio,
  useServizi,
  type ServizioStrutturaDto,
} from '../api/servizi'
import { aggiungiGiorni, formatoInputData, inizioGiornoLocale, parsaInputData } from '../lib/date'
import { tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { CampoData } from './CampoData'

const formattatoreEuro = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

/**
 * Addebito di un servizio extra sul conto dell'ospite, dalla reception, senza aprire la prenotazione
 * (la SPA prenotata al banco, il minibar al check-out). L'importo si somma al totale. Per correggere
 * o togliere un addebito si passa dal dettaglio della prenotazione.
 */
export function AddebitaServizioDialog({
  strutturaId,
  prenotazione,
  onChiudi,
}: {
  strutturaId: string
  prenotazione: PrenotazioneDto
  onChiudi: () => void
}) {
  const toast = useToast()
  const servizi = useServizi(strutturaId)
  const addebita = useAddebitaServizio(strutturaId)
  const arrivo = formatoInputData(inizioGiornoLocale(new Date(prenotazione.checkIn!)))
  const partenza = formatoInputData(inizioGiornoLocale(new Date(prenotazione.checkOut!)))
  const oggi = formatoInputData(new Date())
  // Oggi, se è dentro il soggiorno: un addebito si registra quando succede.
  const giornoProposto = oggi >= arrivo && oggi <= partenza ? oggi : arrivo
  const [servizio, setServizio] = useState<ServizioStrutturaDto | null>(null)
  const [dal, setDal] = useState(giornoProposto)
  const [al, setAl] = useState(partenza)
  const [quantita, setQuantita] = useState('1')

  const aNotte = servizio ? perNotte(servizio.modalita) : false
  const notti = aNotte && dal && al ? nottiTra(dal, al) : 0
  const valido =
    !!servizio &&
    Number(quantita) >= 1 &&
    dal >= arrivo &&
    (aNotte ? al > dal && al <= partenza : dal <= partenza)

  function scegli(s: ServizioStrutturaDto | null) {
    setServizio(s)
    if (!s) return
    setQuantita(String(perPersona(s.modalita) ? Math.max(prenotazione.numeroOspiti ?? 1, 1) : 1))
    if (perNotte(s.modalita)) {
      // Dalla notte di oggi fino alla partenza: il parcheggio preso a metà soggiorno.
      setDal(giornoProposto < partenza ? giornoProposto : arrivo)
      setAl(partenza)
    } else {
      setDal(giornoProposto)
    }
  }

  function conferma() {
    if (!servizio || !valido) {
      toast.errore('Controlla servizio, quantità e date: devono stare dentro il soggiorno.')
      return
    }
    addebita.mutate(
      { prenotazioneId: prenotazione.id, rigaId: null, servizioId: servizio.id, quantita: Number(quantita), dal, al: aNotte ? al : null },
      {
        onSuccess: () => {
          toast.successo(`${servizio.nome} addebitato.`)
          onChiudi()
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  const ospite = [prenotazione.ospiteNome, prenotazione.ospiteCognome].filter(Boolean).join(' ')

  return (
    <Dialog open onClose={addebita.isPending ? undefined : onChiudi} maxWidth="xs" fullWidth>
      <DialogTitle>Addebita un servizio</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '8px !important' }}>
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>
          {[ospite, prenotazione.cameraNome && `camera ${prenotazione.cameraNome}`, prenotazione.numeroPrenotazione && `#${prenotazione.numeroPrenotazione}`]
            .filter(Boolean)
            .join(' · ')}
        </Typography>
        <Autocomplete
          options={(servizi.data ?? []).filter((s) => s.attivo)}
          getOptionLabel={(s) => s.nome}
          value={servizio}
          onChange={(_, s) => scegli(s)}
          loading={servizi.isLoading}
          noOptionsText="Nessun servizio offerto: si attivano in Impostazioni → Servizi"
          renderOption={({ key, ...props }, s) => (
            <li key={key} {...props}>
              {s.nome} — {formattatoreEuro.format(s.prezzo)} {NOME_MODALITA[s.modalita]}
            </li>
          )}
          renderInput={(params) => <TextField {...params} label="Servizio" placeholder="Cerca per nome…" autoFocus />}
        />
        {servizio && (
          <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
            {aNotte ? (
              <>
                <Box sx={{ flex: 1, minWidth: 140 }}>
                  <CampoData
                    label="Dal"
                    size="small"
                    value={dal}
                    onChange={setDal}
                    min={arrivo}
                    max={formatoInputData(aggiungiGiorni(parsaInputData(partenza), -1))}
                    fullWidth
                  />
                </Box>
                <Box sx={{ flex: 1, minWidth: 140 }}>
                  <CampoData label="Al" size="small" value={al} onChange={setAl} min={formatoInputData(aggiungiGiorni(parsaInputData(dal), 1))} max={partenza} fullWidth />
                </Box>
              </>
            ) : (
              <Box sx={{ flex: 1, minWidth: 140 }}>
                <CampoData label="Data" size="small" value={dal} onChange={setDal} min={arrivo} max={partenza} fullWidth />
              </Box>
            )}
            <TextField
              label={perPersona(servizio.modalita) ? 'Persone' : 'Quantità'}
              type="number"
              size="small"
              value={quantita}
              onChange={(e) => {
                const v = e.target.value
                if (v === '' || /^\d+$/.test(v)) setQuantita(v === '' ? '' : String(Math.min(Number(v), 99)))
              }}
              slotProps={{ htmlInput: { min: 1, max: 99 } }}
              sx={{ width: 110 }}
            />
          </Box>
        )}
        {servizio && valido && (
          <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>
            Da addebitare: {formattatoreEuro.format(importoRiga(servizio.modalita, servizio.prezzo, Number(quantita), notti))}
            {aNotte && ` (${notti} ${notti === 1 ? 'notte' : 'notti'})`}
          </Typography>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onChiudi} disabled={addebita.isPending}>
          Annulla
        </Button>
        <Button variant="contained" onClick={conferma} disabled={addebita.isPending || !valido}>
          Addebita
        </Button>
      </DialogActions>
    </Dialog>
  )
}
