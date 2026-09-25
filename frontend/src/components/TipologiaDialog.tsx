import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { ApiError } from '../api/client'
import {
  useCreaTipologia,
  useAggiornaTipologia,
  useAggiornaPulizieTipologia,
  useFasceEtaTipologia,
  useSalvaFasceEtaTipologia,
  type FasciaEtaSupplementoDto,
  type TipologiaCameraDto,
  type TipologiaCameraRequest,
} from '../api/tipologie'
import { useMobile } from '../lib/useMobile'
import { tokens } from '../theme'

/** Come Booking: al massimo tre fasce, così le stesse regole si riportano uguali sul portale. */
const FASCE_MASSIME = 3

type RigaFascia = { etaMin: string; etaMax: string; importo: string }

/** Età digitata a mano in una fascia: solo cifre, oltre 17 diventa 17 come con le frecce (il max del campo ferma solo quelle). Null = tasto ignorato. */
function etaFasciaDigitata(valore: string): string | null {
  if (valore === '') return ''
  if (!/^\d+$/.test(valore)) return null
  return String(Math.min(Number(valore), 17))
}

const inRiga =(f: FasciaEtaSupplementoDto): RigaFascia => ({ etaMin: String(f.etaMin), etaMax: String(f.etaMax), importo: String(f.importoPerNotte) })

interface Props {
  strutturaId: string
  tipologia: TipologiaCameraDto | null
  onClose: () => void
}

export function TipologiaDialog({ strutturaId, tipologia, onClose }: Props) {
  const mobile = useMobile()
  const [nome, setNome] = useState(tipologia?.tipologiaCamera ?? '')
  const [prezzoDefault, setPrezzoDefault] = useState(tipologia?.prezzoDefault != null ? String(tipologia.prezzoDefault) : '')
  const [numeroImplementoPersona, setNumeroImplementoPersona] = useState(String(tipologia?.numeroImplementoPersona ?? 2))
  const [implemento, setImplemento] = useState(String(tipologia?.implemento ?? 0))
  const [spesePulizia, setSpesePulizia] = useState(tipologia?.spesePulizia != null ? String(tipologia.spesePulizia) : '')
  const [animali, setAnimali] = useState(tipologia?.animali != null ? String(tipologia.animali) : '')
  const [cauzione, setCauzione] = useState(tipologia?.cauzione != null ? String(tipologia.cauzione) : '')
  // Vuoto = come la struttura, 0 = nessuna, N = ogni N giorni.
  const [intervalloPulizia, setIntervalloPulizia] = useState(tipologia?.intervalloPuliziaGiorni != null ? String(tipologia.intervalloPuliziaGiorni) : '')
  const [intervalloBiancheria, setIntervalloBiancheria] = useState(tipologia?.intervalloBiancheriaGiorni != null ? String(tipologia.intervalloBiancheriaGiorni) : '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaTipologia(strutturaId)
  const aggiorna = useAggiornaTipologia(strutturaId)
  const aggiornaPulizie = useAggiornaPulizieTipologia(strutturaId)
  const salvaFasce = useSalvaFasceEtaTipologia(strutturaId)
  const fasceSalvate = useFasceEtaTipologia(strutturaId, tipologia?.id ?? null)
  // Null finché l'operatore non le tocca: si mostrano quelle salvate e al salvataggio non si
  // rimandano, così un caricamento non ancora arrivato non può cancellarle.
  const [fasceModificate, setFasceModificate] = useState<RigaFascia[] | null>(null)
  const fasce = fasceModificate ?? (fasceSalvate.data ?? []).map(inRiga)
  const inCorso = crea.isPending || aggiorna.isPending || aggiornaPulizie.isPending || salvaFasce.isPending

  function modificaFascia(i: number, campo: keyof RigaFascia, valore: string) {
    setFasceModificate(fasce.map((f, j) => (j === i ? { ...f, [campo]: valore } : f)))
  }

  function salva() {
    if (nome.trim() === '') {
      setErrore('Il nome della tipologia è obbligatorio.')
      return
    }
    if (fasceModificate?.some((f) => f.etaMin.trim() === '' || f.etaMax.trim() === '' || f.importo.trim() === '')) {
      setErrore('Completa età e importo di ogni fascia, o togli quelle che non servono.')
      return
    }
    setErrore(null)

    const request: TipologiaCameraRequest = {
      tipologiaCamera: nome.trim(),
      prezzoDefault: prezzoDefault.trim() === '' ? null : Number(prezzoDefault),
      numeroImplementoPersona: Number(numeroImplementoPersona) || 0,
      implemento: Number(implemento) || 0,
      spesePulizia: spesePulizia.trim() === '' ? null : Number(spesePulizia),
      animali: animali.trim() === '' ? null : Number(animali),
      cauzione: cauzione.trim() === '' ? null : Number(cauzione),
      // Non editabili da questo dialog (v. il dialog camera della pagina Servizi OTA) — passati
      // invariati per non azzerarli ad ogni salvataggio della scheda tipologia principale.
      codiceCameraWubook: tipologia?.codiceCameraWubook ?? null,
      wubookSoloWoodoo: tipologia?.wubookSoloWoodoo ?? false,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    // Fasce d'età e frequenze delle pulizie hanno endpoint loro (vedi useAggiornaPulizieTipologia): si
    // salvano dopo la tipologia, sull'id che la tipologia ha appena ricevuto o che aveva già. Le fasce
    // solo se toccate.
    const salvaFasceSeModificate = (tipologiaId: string) =>
      fasceModificate === null
        ? onClose()
        : salvaFasce.mutate(
            {
              tipologiaId,
              fasce: fasceModificate.map((f) => ({ etaMin: Number(f.etaMin), etaMax: Number(f.etaMax), importoPerNotte: Number(f.importo) })),
            },
            { onSuccess: onClose, onError },
          )

    const salvaPulizie = (tipologiaId: string) =>
      aggiornaPulizie.mutate(
        {
          tipologiaId,
          intervalloPuliziaGiorni: intervalloPulizia.trim() === '' ? null : Number(intervalloPulizia),
          intervalloBiancheriaGiorni: intervalloBiancheria.trim() === '' ? null : Number(intervalloBiancheria),
        },
        { onSuccess: () => salvaFasceSeModificate(tipologiaId), onError },
      )

    if (tipologia) {
      aggiorna.mutate({ tipologiaId: tipologia.id, request }, { onSuccess: () => salvaPulizie(tipologia.id), onError })
    } else {
      crea.mutate(request, { onSuccess: (creata) => salvaPulizie(creata.id), onError })
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>{tipologia ? 'Modifica tipologia' : 'Nuova tipologia'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <TextField label="Nome tipologia" value={nome} onChange={(e) => setNome(e.target.value)} required disabled={inCorso} autoFocus />

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Prezzo default (€/notte)" type="number" value={prezzoDefault} onChange={(e) => setPrezzoDefault(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Cauzione (€)" type="number" value={cauzione} onChange={(e) => setCauzione(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField
            label="Ospiti inclusi nel prezzo"
            type="number"
            value={numeroImplementoPersona}
            onChange={(e) => setNumeroImplementoPersona(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0 } }}
            helperText="Sopra questa soglia si applica il supplemento per notte"
          />
          <TextField label="Supplemento per persona extra (€/notte)" type="number" value={implemento} onChange={(e) => setImplemento(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
          <Box>
            <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>Supplemento per i bambini</Typography>
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Per i bambini oltre gli ospiti inclusi: importo a notte per fascia d&apos;età, età comprese, 0 = gratis. Dai 18 anni, o per
              un&apos;età che non rientra in nessuna fascia, si paga il supplemento pieno. Al massimo {FASCE_MASSIME} fasce, come su Booking:
              lì vanno impostate a parte.
            </Typography>
          </Box>
          {fasce.map((f, i) => (
            <Box key={i} sx={{ display: 'flex', gap: 1.5, alignItems: 'center' }}>
              <TextField
                label="Da (anni)"
                type="number"
                size="small"
                value={f.etaMin}
                onChange={(e) => {
                  const valore = etaFasciaDigitata(e.target.value)
                  if (valore !== null) modificaFascia(i, 'etaMin', valore)
                }}
                disabled={inCorso}
                slotProps={{ htmlInput: { min: 0, max: 17 } }}
                sx={{ flex: 1 }}
              />
              <TextField
                label="A (anni)"
                type="number"
                size="small"
                value={f.etaMax}
                onChange={(e) => {
                  const valore = etaFasciaDigitata(e.target.value)
                  if (valore !== null) modificaFascia(i, 'etaMax', valore)
                }}
                disabled={inCorso}
                slotProps={{ htmlInput: { min: 0, max: 17 } }}
                sx={{ flex: 1 }}
              />
              <TextField
                label="€/notte"
                type="number"
                size="small"
                value={f.importo}
                onChange={(e) => modificaFascia(i, 'importo', e.target.value)}
                disabled={inCorso}
                slotProps={{ htmlInput: { min: 0 } }}
                sx={{ flex: 1 }}
              />
              <IconButton aria-label="Togli fascia" onClick={() => setFasceModificate(fasce.filter((_, j) => j !== i))} disabled={inCorso}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Box>
          ))}
          <Box>
            <Button
              size="small"
              onClick={() => setFasceModificate([...fasce, { etaMin: '', etaMax: '', importo: '' }])}
              disabled={inCorso || fasce.length >= FASCE_MASSIME || (!!tipologia && fasceSalvate.isLoading)}
            >
              Aggiungi fascia d&apos;età
            </Button>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Spese pulizia (€)" type="number" value={spesePulizia} onChange={(e) => setSpesePulizia(e.target.value)} fullWidth disabled={inCorso} />
          <TextField label="Supplemento animali (€)" type="number" value={animali} onChange={(e) => setAnimali(e.target.value)} fullWidth disabled={inCorso} />
        </Box>

        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField
            label="Pulizia durante il soggiorno ogni (giorni)"
            type="number"
            value={intervalloPulizia}
            onChange={(e) => setIntervalloPulizia(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0, max: 30 } }}
            helperText="Vuoto = come la struttura. 0 = nessuna"
          />
          <TextField
            label="Cambio biancheria ogni (giorni)"
            type="number"
            value={intervalloBiancheria}
            onChange={(e) => setIntervalloBiancheria(e.target.value)}
            fullWidth
            disabled={inCorso}
            slotProps={{ htmlInput: { min: 0, max: 30 } }}
            helperText="Vuoto = come la struttura. 0 = nessuno"
          />
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {tipologia ? 'Salva modifiche' : 'Crea tipologia'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
