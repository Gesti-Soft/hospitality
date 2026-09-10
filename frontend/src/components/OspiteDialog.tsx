import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
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
import { useFatturaPerPrenotazione, type DatiFatturaDto } from '../api/fatturazione'
import type { PrenotazioneDto } from '../api/prenotazioni'
import {
  Sesso,
  useOspite,
  useSalvaSchedaOspiti,
  type MembroOspiteRequest,
  type OspiteDto,
  type SalvaSchedaOspitiRequest,
} from '../api/ospiti'
import { useDocumenti, useStati, useTipiAlloggiato } from '../api/riferimenti'
import { differenzaGiorni, formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { SelectComune } from './SelectComune'
import { fontDisplay, tokens } from '../theme'
import { usePuoScrivere } from '../permessi/usePuoScrivere'

interface Props {
  strutturaId: string
  prenotazione: PrenotazioneDto
  onClose: () => void
  /** Se passata, mostra un bottone "Apri prenotazione" — assente quando non ha senso (es. non c'è dove navigare). */
  onApriPrenotazione?: () => void
  /** Se passata, mostra un bottone "Genera fattura" — solo per prenotazioni in corso o completate (vedi OspitiPage). */
  onGeneraFattura?: () => void
  /** Se passata, viene invocata subito dopo il salvataggio riuscito della scheda (es. per eseguire il check-in solo a salvataggio avvenuto, mai chiudendo senza salvare). */
  dopoSalvataggio?: () => void
}

export function OspiteDialog({ strutturaId, prenotazione, onClose, onApriPrenotazione, onGeneraFattura, dopoSalvataggio }: Props) {
  const mobile = useMobile()
  const ospite = useOspite(strutturaId, prenotazione.id)
  const fattura = useFatturaPerPrenotazione(strutturaId, prenotazione.id)

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth fullScreen={mobile}>
      <DialogTitle>
        Scheda ospiti — {prenotazione.numeroPrenotazione ? `#${prenotazione.numeroPrenotazione}` : prenotazione.cameraNome}
      </DialogTitle>
      {ospite.isLoading && (
        <DialogContent>
          <Skeleton variant="rounded" height={320} />
        </DialogContent>
      )}
      {!ospite.isLoading && (
        <SchedaOspitiForm
          strutturaId={strutturaId}
          prenotazione={prenotazione}
          ospite={ospite.data ?? null}
          onClose={onClose}
          onApriPrenotazione={onApriPrenotazione}
          onGeneraFattura={onGeneraFattura}
          dopoSalvataggio={dopoSalvataggio}
          fatturaGenerata={fattura.data ?? null}
        />
      )}
    </Dialog>
  )
}

const permanenzaDefault = (p: PrenotazioneDto) => (p.checkIn && p.checkOut ? Math.max(differenzaGiorni(new Date(p.checkOut), new Date(p.checkIn)), 1) : 1)

/** Select con autocompletamento sopra un elenco statico già caricato (Stati, Documenti, Tipo ospite). */
function SelectRiferimento({
  label,
  value,
  onChange,
  opzioni,
  loading,
  disabled,
  size,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  opzioni: string[]
  loading: boolean
  disabled?: boolean
  size?: 'small' | 'medium'
}) {
  // Se il valore salvato non combacia con nessuna opzione ufficiale (es. dato storico digitato a
  // mano), lo mostriamo comunque come opzione extra invece di farlo sparire silenziosamente.
  const opzioniConValoreCorrente = value && !opzioni.includes(value) ? [value, ...opzioni] : opzioni

  return (
    <Autocomplete
      fullWidth
      size={size}
      disabled={disabled}
      loading={loading}
      options={opzioniConValoreCorrente}
      value={value || null}
      onChange={(_, v) => onChange(v ?? '')}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          slotProps={{
            ...params.slotProps,
            input: {
              ...params.slotProps.input,
              endAdornment: (
                <>
                  {loading && <CircularProgress color="inherit" size={16} />}
                  {params.slotProps.input.endAdornment}
                </>
              ),
            },
          }}
        />
      )}
    />
  )
}

function SchedaOspitiForm({
  strutturaId,
  prenotazione,
  ospite,
  onClose,
  onApriPrenotazione,
  onGeneraFattura,
  dopoSalvataggio,
  fatturaGenerata,
}: {
  strutturaId: string
  prenotazione: PrenotazioneDto
  ospite: OspiteDto | null
  onClose: () => void
  onApriPrenotazione?: () => void
  onGeneraFattura?: () => void
  dopoSalvataggio?: () => void
  fatturaGenerata: DatiFatturaDto | null
}) {
  const mobile = useMobile()
  const puoScrivere = usePuoScrivere('reservationWrite')
  const puoFatturare = usePuoScrivere('financeWrite')
  const stati = useStati()
  const documenti = useDocumenti()
  const tipiAlloggiato = useTipiAlloggiato()

  const opzioniStati = (stati.data ?? []).map((s) => s.descrizione)
  const opzioniDocumenti = (documenti.data ?? []).map((d) => d.descrizione)
  // FAMILIARE/MEMBRO GRUPPO (codici 19/20) sono classificazioni valide solo per gli "altri ospiti"
  // della schedina, mai per il capofamiglia — stesso filtro del gestionale legacy.
  const opzioniTipoOspite = (tipiAlloggiato.data ?? []).filter((t) => t.codice !== '19' && t.codice !== '20').map((t) => t.descrizione)

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

  // Un "Ospite singolo" non ha altri membri per definizione — la sezione "Altri ospiti" non ha
  // senso e va nascosta; per Capo famiglia/Capo gruppo, invece, serve almeno un membro (controllato
  // al salvataggio più sotto), stessa regola del gestionale legacy.
  const isOspiteSingolo = tipoOspite.trim().toUpperCase() === 'OSPITE SINGOLO'

  // _key identifica la riga in modo stabile per React (a differenza dell'indice nell'array, non
  // cambia quando una riga precedente viene rimossa) — altrimenti, riusando la stessa istanza di
  // SelectComune per una riga diversa, il testo digitato al suo interno resterebbe quello vecchio.
  const [membri, setMembri] = useState<(MembroOspiteRequest & { _key: string })[]>(
    (ospite?.membri ?? []).map((m) => ({
      _key: m.id ?? crypto.randomUUID(),
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
        _key: crypto.randomUUID(),
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
        postoLetto: true,
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
    if (!isOspiteSingolo && membri.length === 0) {
      setErrore('Aggiungi almeno un ospite, oppure imposta "Tipo ospite" su Ospite singolo.')
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
      membri: membri.map(({ _key, ...m }) => m),
    }

    salva.mutate(request, {
      onSuccess: () => {
        dopoSalvataggio?.()
        onClose()
      },
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}

        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 13.5 }}>Capofamiglia</Typography>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth required disabled={salva.isPending} />
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth required disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <CampoData label="Data di nascita" value={dataNascita} onChange={setDataNascita} fullWidth disabled={salva.isPending} />
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

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <SelectRiferimento label="Cittadinanza" value={cittadinanza} onChange={setCittadinanza} opzioni={opzioniStati} loading={stati.isLoading} disabled={salva.isPending} />
          <SelectRiferimento label="Stato di nascita" value={statoNascita} onChange={setStatoNascita} opzioni={opzioniStati} loading={stati.isLoading} disabled={salva.isPending} />
          <SelectComune label="Comune di nascita" value={luogoNascita} onChange={setLuogoNascita} disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <SelectComune label="Comune di residenza" value={luogoResidenza} onChange={setLuogoResidenza} disabled={salva.isPending} />
          <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <SelectRiferimento label="Tipo documento" value={documento} onChange={setDocumento} opzioni={opzioniDocumenti} loading={documenti.isLoading} disabled={salva.isPending} />
          <TextField label="Numero documento" value={numeroDocumento} onChange={(e) => setNumeroDocumento(e.target.value)} fullWidth disabled={salva.isPending} />
          <SelectComune label="Rilasciato da" value={rilascioDocumento} onChange={setRilascioDocumento} disabled={salva.isPending} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2, alignItems: mobile ? 'stretch' : 'center' }}>
          <SelectRiferimento
            label="Tipo ospite (classificazione schedina)"
            value={tipoOspite}
            onChange={setTipoOspite}
            opzioni={opzioniTipoOspite}
            loading={tipiAlloggiato.isLoading}
            disabled={salva.isPending}
          />
          <FormControlLabel
            control={<Checkbox checked={esenteDaTassa} onChange={(e) => setEsenteDaTassa(e.target.checked)} disabled={salva.isPending} />}
            label="Esente tassa di soggiorno"
            sx={{ whiteSpace: 'nowrap' }}
          />
        </Box>

        {!isOspiteSingolo && (
          <>
            <Divider sx={{ mt: 1 }} />

            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 13.5 }}>Altri ospiti ({membri.length})</Typography>
              {puoScrivere && (
                <Button size="small" onClick={aggiungiMembro} disabled={salva.isPending}>
                  + Aggiungi ospite
                </Button>
              )}
            </Box>

            {membri.map((m, indice) => (
              <Box key={m._key} sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, p: 1.75, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
                <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
                  <TextField label="Cognome" value={m.cognome ?? ''} onChange={(e) => aggiornaMembro(indice, { cognome: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
                  <TextField label="Nome" value={m.nome ?? ''} onChange={(e) => aggiornaMembro(indice, { nome: e.target.value })} fullWidth size="small" disabled={salva.isPending} />
                  <CampoData
                    label="Data di nascita"
                    value={m.dataNascita ? formatoInputData(new Date(m.dataNascita)) : ''}
                    onChange={(v) => aggiornaMembro(indice, { dataNascita: v === '' ? null : isoLocale(parsaInputData(v)) })}
                    fullWidth
                    size="small"
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
                  {puoScrivere && (
                    <IconButton size="small" onClick={() => rimuoviMembro(indice)} disabled={salva.isPending}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  )}
                </Box>
                <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
                  <SelectRiferimento
                    label="Cittadinanza"
                    value={m.cittadinanza ?? ''}
                    onChange={(v) => aggiornaMembro(indice, { cittadinanza: v })}
                    opzioni={opzioniStati}
                    loading={stati.isLoading}
                    disabled={salva.isPending}
                    size="small"
                  />
                  <SelectRiferimento
                    label="Stato di nascita"
                    value={m.statoNascita ?? ''}
                    onChange={(v) => aggiornaMembro(indice, { statoNascita: v })}
                    opzioni={opzioniStati}
                    loading={stati.isLoading}
                    disabled={salva.isPending}
                    size="small"
                  />
                  <SelectComune label="Comune di nascita" value={m.luogoNascita ?? ''} onChange={(v) => aggiornaMembro(indice, { luogoNascita: v })} disabled={salva.isPending} size="small" />
                  <SelectComune label="Comune di residenza" value={m.luogoResidenza ?? ''} onChange={(v) => aggiornaMembro(indice, { luogoResidenza: v })} disabled={salva.isPending} size="small" />
                </Box>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1.5 }}>
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
          </>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5, flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1, mr: 'auto' }}>
          {onApriPrenotazione && (
            <Button onClick={onApriPrenotazione} disabled={salva.isPending}>
              Apri prenotazione
            </Button>
          )}
          {fatturaGenerata ? (
            <Chip
              size="small"
              label={`Fattura generata — n. ${fatturaGenerata.numeroDocumento}/${fatturaGenerata.anno}`}
              sx={{ bgcolor: tokens.ok600, color: '#fff', fontWeight: 700, alignSelf: 'center' }}
            />
          ) : (
            onGeneraFattura &&
            puoFatturare && (
              <Button onClick={onGeneraFattura} disabled={salva.isPending}>
                Genera fattura
              </Button>
            )
          )}
        </Box>
        <Button onClick={onClose} disabled={salva.isPending}>
          Chiudi
        </Button>
        {puoScrivere && (
          <Button variant="contained" color="primary" onClick={onSalva} disabled={salva.isPending}>
            Salva scheda
          </Button>
        )}
      </DialogActions>
    </>
  )
}
