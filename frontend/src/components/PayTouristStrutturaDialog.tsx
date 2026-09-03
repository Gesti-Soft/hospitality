import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import ListItemText from '@mui/material/ListItemText'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import { ApiError } from '../api/client'
import {
  useCreaPayTouristStruttura,
  useAggiornaPayTouristStruttura,
  usePayTouristStruttureDisponibili,
  type PayTouristStrutturaDto,
  type PayTouristStrutturaRemotaDto,
  type PayTouristStrutturaRequest,
} from '../api/integrazioni'
import type { TipologiaCameraDto } from '../api/tipologie'

interface Props {
  strutturaId: string
  strutturaPayTourist: PayTouristStrutturaDto | null
  tipologie: TipologiaCameraDto[]
  onClose: () => void
}

export function PayTouristStrutturaDialog({ strutturaId, strutturaPayTourist, tipologie, onClose }: Props) {
  const [nome, setNome] = useState(strutturaPayTourist?.nome ?? '')
  const [idStrutturaPaytourist, setIdStrutturaPaytourist] = useState(strutturaPayTourist?.idStrutturaPaytourist != null ? String(strutturaPayTourist.idStrutturaPaytourist) : '')
  const [tipologieIds, setTipologieIds] = useState<string[]>(strutturaPayTourist?.tipologieIds ?? [])
  const [errore, setErrore] = useState<string | null>(null)
  const [esitoConnessione, setEsitoConnessione] = useState<{ ok: boolean; errore: string | null } | null>(null)

  const crea = useCreaPayTouristStruttura(strutturaId)
  const aggiorna = useAggiornaPayTouristStruttura(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending
  const salvato = esitoConnessione !== null

  // Solo in creazione: propone le strutture abilitate su PayTourist (per Token già configurato) invece
  // di far digitare a mano lo structure_id — in modifica i campi restano manuali, la struttura è già associata.
  const struttureDisponibili = usePayTouristStruttureDisponibili(strutturaId, !strutturaPayTourist)

  function seleziona(struttura: PayTouristStrutturaRemotaDto | null) {
    if (!struttura) {
      return
    }
    setNome(struttura.nome)
    setIdStrutturaPaytourist(String(struttura.id))
  }

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome è obbligatorio.')
      return
    }
    setErrore(null)

    const request: PayTouristStrutturaRequest = {
      nome: nome.trim(),
      idStrutturaPaytourist: idStrutturaPaytourist.trim() === '' ? null : Number(idStrutturaPaytourist),
      tipologieIds,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    const onSuccess = (risultato: { connessioneOk: boolean; connessioneErrore: string | null }) =>
      setEsitoConnessione({ ok: risultato.connessioneOk, errore: risultato.connessioneErrore })

    if (strutturaPayTourist) {
      aggiorna.mutate({ payTouristStrutturaId: strutturaPayTourist.id, request }, { onSuccess, onError })
    } else {
      crea.mutate(request, { onSuccess, onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{strutturaPayTourist ? 'Modifica struttura PayTourist' : 'Nuova struttura PayTourist'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>
        {esitoConnessione && (
          <Alert severity={esitoConnessione.ok ? 'success' : 'warning'}>
            {esitoConnessione.ok
              ? 'Salvato — connessione a PayTourist verificata con successo.'
              : `Salvato, ma la verifica della connessione non è riuscita: ${esitoConnessione.errore ?? 'errore sconosciuto'}`}
          </Alert>
        )}

        {!strutturaPayTourist && !struttureDisponibili.isError && (
          <Autocomplete
            options={struttureDisponibili.data ?? []}
            getOptionLabel={(s) => `${s.nome} (#${s.id})`}
            onChange={(_, s) => seleziona(s)}
            loading={struttureDisponibili.isPending}
            loadingText="Caricamento strutture da PayTourist..."
            noOptionsText="Nessuna struttura trovata sul tuo account PayTourist."
            disabled={inCorso}
            renderInput={(params) => (
              <TextField {...params} label="Struttura PayTourist" autoFocus helperText="Seleziona dall'elenco delle strutture abilitate sul tuo account PayTourist: Nome e Id qui sotto si compilano da soli." />
            )}
          />
        )}
        {!strutturaPayTourist && struttureDisponibili.isError && (
          <Alert severity="info">Impossibile recuperare l'elenco strutture da PayTourist ({struttureDisponibili.error instanceof ApiError ? struttureDisponibili.error.message : 'errore'}) — inserisci i dati a mano qui sotto.</Alert>
        )}

        <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso || salvato} />
        <TextField
          label="Id struttura PayTourist"
          type="number"
          value={idStrutturaPaytourist}
          onChange={(e) => setIdStrutturaPaytourist(e.target.value)}
          disabled={inCorso || salvato}
          helperText="structure_id fornito da PayTourist"
        />

        <TextField
          select
          label="Tipologie camera instradate"
          value={tipologieIds}
          onChange={(e) => setTipologieIds(typeof e.target.value === 'string' ? e.target.value.split(',') : (e.target.value as string[]))}
          slotProps={{ select: { multiple: true, renderValue: (selected) => (selected as string[]).length + ' selezionate' } }}
          disabled={inCorso || salvato}
        >
          {tipologie.map((t) => (
            <MenuItem key={t.id} value={t.id}>
              <Checkbox checked={tipologieIds.includes(t.id)} size="small" />
              <ListItemText primary={t.tipologiaCamera} />
            </MenuItem>
          ))}
        </TextField>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        {!salvato && (
          <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
            {strutturaPayTourist ? 'Salva modifiche' : 'Crea struttura'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  )
}
