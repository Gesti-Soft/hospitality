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
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { ApiError } from '../api/client'
import type { PrenotazioneDto } from '../api/prenotazioni'
import {
  Sesso,
  useOspite,
  useSalvaSchedaOspiti,
  type MembroOspiteRequest,
  type OspiteDto,
  type SalvaSchedaOspitiRequest,
} from '../api/ospiti'
import { differenzaGiorni, formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { fontDisplay, tokens } from '../theme'

interface Props {
  strutturaId: string
  prenotazione: PrenotazioneDto
  onClose: () => void
}

export function OspiteDialog({ strutturaId, prenotazione, onClose }: Props) {
  const ospite = useOspite(strutturaId, prenotazione.id)

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        Scheda ospiti — {prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : prenotazione.cameraNome}
      </DialogTitle>
      {ospite.isLoading && (
        <DialogContent>
          <Skeleton variant="rounded" height={320} />
        </DialogContent>
      )}
      {!ospite.isLoading && (
        <SchedaOspitiForm strutturaId={strutturaId} prenotazione={prenotazione} ospite={ospite.data ?? null} onClose={onClose} />
      )}
    </Dialog>
  )
}

const permanenzaDefault = (p: PrenotazioneDto) => (p.checkIn && p.checkOut ? Math.max(differenzaGiorni(new Date(p.checkOut), new Date(p.checkIn)), 1) : 1)

function SchedaOspitiForm({
  strutturaId,
  prenotazione,
  ospite,
  onClose,
}: {
  strutturaId: string
  prenotazione: PrenotazioneDto
  ospite: OspiteDto | null
  onClose: () => void
}) {
  const [tipoOspite, setTipoOspite] = useState(ospite?.tipoOspite ?? '')
  const [permanenza, setPermanenza] = useState(String(ospite?.permanenza ?? permanenzaDefault(prenotazione)))
  const [dataNascita, setDataNascita] = useState(ospite?.dataNascita ? formatoInputData(new Date(ospite.dataNascita)) : '')
  const [sesso, setSesso] = useState<string>(ospite?.sesso != null ? String(ospite.sesso) : '')
  const [cognome, setCognome] = useState(ospite?.cognome ?? '')
  const [nome, setNome] = useState(ospite?.nome ?? '')
  const [cittadinanza, setCittadinanza] = useState(ospite?.cittadinanza ?? '')
  const [luogoNascita, setLuogoNascita] = useState(ospite?.luogoNascita ?? '')
  const [statoNascita, setStatoNascita] = useState(ospite?.statoNascita ?? '')
  const [luogoResidenza, setLuogoResidenza] = useState(ospite?.luogoResidenza ?? '')
  const [email, setEmail] = useState(ospite?.email ?? '')
  const [documento, setDocumento] = useState(ospite?.documento ?? '')
  const [numeroDocumento, setNumeroDocumento] = useState(ospite?.numeroDocumento ?? '')
  const [rilascioDocumento, setRilascioDocumento] = useState(ospite?.rilascioDocumento ?? '')
  const [esenteDaTassa, setEsenteDaTassa] = useState(ospite?.esenteDaTassa ?? false)

  const [membri, setMembri] = useState<MembroOspiteRequest[]>(
    (ospite?.membri ?? []).map((m) => ({
      id: m.id,
      cameraId: m.cameraId,
      permanenza: m.permanenza,
      dataNascita: m.dataNascita,
      sesso: m.sesso,
      cognome: m.cognome,
      nome: m.nome,
      cittadinanza: m.cittadinanza,
      luogoNascita: m.luogoNascita,
      statoNascita: m.statoNascita,
      luogoResidenza: m.luogoResidenza,
      postoLetto: m.postoLetto,
      esenteDaTassa: m.esenteDaTassa,
    })),
  )

  const [errore, setErrore] = useState<string | null>(null)

  const salva = useSalvaSchedaOspiti(strutturaId, prenotazione.id)

  function aggiornaMembro(indice: number, patch: Partial<MembroOspiteRequest>) {
    setMembri((prec) => prec.map((m, i) => (i === indice ? { ...m, ...patch } : m)))
  }

  function aggiungiMembro() {
    setMembri((prec) => [
      ...prec,
      {
        id: null,
        cameraId: prenotazione.cameraId,
        permanenza: Number(permanenza) || null,
        dataNascita: null,
        sesso: null,
        cognome: '',
        nome: '',
        cittadinanza: '',
        luogoNascita: '',
        statoNascita: '',
        luogoResidenza: '',
        postoLetto: false,
        esenteDaTassa: false,
      },
    ])
  }

  function rimuoviMembro(indice: number) {
    setMembri((prec) => prec.filter((_, i) => i !== indice))
  }

  function onSalva() {
    if (cognome.trim() === '' || nome.trim() === '') {
      setErrore('Cognome e nome del capofamiglia sono obbligatori.')
      return
    }
    setErrore(null)

    const request: SalvaSchedaOspitiRequest = {
      tipoOspite: tipoOspite.trim() === '' ? null : tipoOspite.trim(),
      permanenza: permanenza.trim() === '' ? null : Number(permanenza),
      dataNascita: dataNascita === '' ? null : isoLocale(parsaInputData(dataNascita)),
      sesso: sesso === '' ? null : (Number(sesso) as Sesso),
      cognome: cognome.trim(),
      nome: nome.trim(),
      cittadinanza: cittadinanza.trim() === '' ? null : cittadinanza.trim(),
      luogoNascita: luogoNascita.trim() === '' ? null : luogoNascita.trim(),
      statoNascita: statoNascita.trim() === '' ? null : statoNascita.trim(),
      luogoResidenza: luogoResidenza.trim() === '' ? null : luogoResidenza.trim(),
      email: email.trim() === '' ? null : email.trim(),
      documento: documento.trim() === '' ? null : documento.trim(),
      numeroDocumento: numeroDocumento.trim() === '' ? null : numeroDocumento.trim(),
      rilascioDocumento: rilascioDocumento.trim() === '' ? null : rilascioDocumento.trim(),
      esenteDaTassa,
      membri,
    }

    salva.mutate(request, {
      onSuccess: onClose,
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 13.5 }}>Capofamiglia</Typography>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth required disabled={salva.isPending} />
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth required disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField
            label="Data di nascita"
            type="date"
            value={dataNascita}
            onChange={(e) => setDataNascita(e.target.value)}
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
            disabled={salva.isPending}
          />
          <TextField select label="Sesso" value={sesso} onChange={(e) => setSesso(e.target.value)} fullWidth disabled={salva.isPending}>
            <MenuItem value="">—</MenuItem>
            <MenuItem value={String(Sesso.Maschio)}>Maschio</MenuItem>
            <MenuItem value={String(Sesso.Femmina)}>Femmina</MenuItem>
          </TextField>
          <TextField
            label="Permanenza (notti)"
            type="number"
            value={permanenza}
            onChange={(e) => setPermanenza(e.target.value)}
            fullWidth
            disabled={salva.isPending}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Cittadinanza" value={cittadinanza} onChange={(e) => setCittadinanza(e.target.value)} fullWidth disabled={salva.isPending} />
          <TextField label="Stato di nascita" value={statoNascita} onChange={(e) => setStatoNascita(e.target.value)} fullWidth disabled={salva.isPending} />
          <TextField label="Comune di nascita" value={luogoNascita} onChange={(e) => setLuogoNascita(e.target.value)} fullWidth disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Comune di residenza" value={luogoResidenza} onChange={(e) => setLuogoResidenza(e.target.value)} fullWidth disabled={salva.isPending} />
          <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2 }}>
          <TextField label="Tipo documento" value={documento} onChange={(e) => setDocumento(e.target.value)} fullWidth disabled={salva.isPending} />
          <TextField label="Numero documento" value={numeroDocumento} onChange={(e) => setNumeroDocumento(e.target.value)} fullWidth disabled={salva.isPending} />
          <TextField label="Rilasciato da" value={rilascioDocumento} onChange={(e) => setRilascioDocumento(e.target.value)} fullWidth disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <TextField label="Tipo ospite (classificazione schedina)" value={tipoOspite} onChange={(e) => setTipoOspite(e.target.value)} fullWidth disabled={salva.isPending} />
          <FormControlLabel
            control={<Checkbox checked={esenteDaTassa} onChange={(e) => setEsenteDaTassa(e.target.checked)} disabled={salva.isPending} />}
            label="Esente tassa di soggiorno"
            sx={{ whiteSpace: 'nowrap' }}
          />
        </Box>

        <Divider sx={{ mt: 1 }} />

        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 13.5 }}>Altri ospiti ({membri.length})</Typography>
          <Button size="small" onClick={aggiungiMembro} disabled={salva.isPending}>
            + Aggiungi ospite
          </Button>
        </Box>

        {membri.map((m, indice) => (
          <Box key={indice} sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, p: 1.75, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label="Cognome" value={m.cognome ?? ''} onChange={(e) => aggiornaMembro(indice, { cognome: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
              <TextField label="Nome" value={m.nome ?? ''} onChange={(e) => aggiornaMembro(indice, { nome: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
              <TextField
                label="Data di nascita"
                type="date"
                value={m.dataNascita ? formatoInputData(new Date(m.dataNascita)) : ''}
                onChange={(e) => aggiornaMembro(indice, { dataNascita: e.target.value === '' ? null : isoLocale(parsaInputData(e.target.value)) })}
                fullWidth
                size="small"
                slotProps={{ inputLabel: { shrink: true } }}
                disabled={salva.isPending}
              />
              <TextField
                select
                label="Sesso"
                value={m.sesso != null ? String(m.sesso) : ''}
                onChange={(e) => aggiornaMembro(indice, { sesso: e.target.value === '' ? null : (Number(e.target.value) as Sesso) })}
                sx={{ minWidth: 130 }}
                size="small"
                disabled={salva.isPending}
              >
                <MenuItem value="">—</MenuItem>
                <MenuItem value={String(Sesso.Maschio)}>Maschio</MenuItem>
                <MenuItem value={String(Sesso.Femmina)}>Femmina</MenuItem>
              </TextField>
              <IconButton size="small" onClick={() => rimuoviMembro(indice)} disabled={salva.isPending}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Box>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label="Cittadinanza" value={m.cittadinanza ?? ''} onChange={(e) => aggiornaMembro(indice, { cittadinanza: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
              <TextField label="Stato di nascita" value={m.statoNascita ?? ''} onChange={(e) => aggiornaMembro(indice, { statoNascita: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
              <TextField label="Comune di nascita" value={m.luogoNascita ?? ''} onChange={(e) => aggiornaMembro(indice, { luogoNascita: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
              <TextField label="Comune di residenza" value={m.luogoResidenza ?? ''} onChange={(e) => aggiornaMembro(indice, { luogoResidenza: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
            </Box>
            <Box sx={{ display: 'flex', gap: 3 }}>
              <FormControlLabel
                control={<Checkbox size="small" checked={m.postoLetto ?? false} onChange={(e) => aggiornaMembro(indice, { postoLetto: e.target.checked })} disabled={salva.isPending} />}
                label="Occupa un posto letto"
              />
              <FormControlLabel
                control={<Checkbox size="small" checked={m.esenteDaTassa} onChange={(e) => aggiornaMembro(indice, { esenteDaTassa: e.target.checked })} disabled={salva.isPending} />}
                label="Esente tassa di soggiorno"
              />
            </Box>
          </Box>
        ))}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={salva.isPending}>
          Chiudi
        </Button>
        <Button variant="contained" color="secondary" onClick={onSalva} disabled={salva.isPending}>
          Salva scheda
        </Button>
      </DialogActions>
    </>
  )
}
