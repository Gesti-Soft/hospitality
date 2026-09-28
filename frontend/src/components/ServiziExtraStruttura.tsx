import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { ApiError } from '../api/client'
import {
  ModalitaPrezzoServizio,
  NOME_MODALITA,
  useAggiornaServizio,
  useCreaServizio,
  useEliminaServizio,
  useServizi,
  type ServizioStrutturaDto,
} from '../api/servizi'
import { AliquotaIva, type NaturaIva } from '../api/fatturazione'
import { useMobile } from '../lib/useMobile'
import { fontDisplay, tokens } from '../theme'
import { useToast } from '../toast/ToastContext'
import { ConfirmDialog } from './ConfirmDialog'
import { ETICHETTA_NATURA } from './FatturaDialog'

const MODALITA = Object.values(ModalitaPrezzoServizio) as ModalitaPrezzoServizio[]
const ALIQUOTE_POSITIVE = (Object.values(AliquotaIva) as AliquotaIva[]).filter((a) => a > 0)
const NATURE = Object.keys(ETICHETTA_NATURA).map(Number) as NaturaIva[]

/** Un solo campo per l'IVA: '' = come la struttura, "a22" = aliquota, "n22" = 0% con la natura. */
function codiceIva(aliquota: AliquotaIva | null, natura: NaturaIva | null): string {
  if (aliquota != null && aliquota > 0) return `a${aliquota}`
  if (natura != null) return `n${natura}`
  return ''
}

function daCodiceIva(codice: string): { aliquotaIva: AliquotaIva | null; natura: NaturaIva | null } {
  if (codice.startsWith('a')) return { aliquotaIva: Number(codice.slice(1)) as AliquotaIva, natura: null }
  if (codice.startsWith('n')) return { aliquotaIva: AliquotaIva.Iva0, natura: Number(codice.slice(1)) as NaturaIva }
  return { aliquotaIva: null, natura: null }
}

/**
 * Servizi extra creati dall'utente (escursioni, parcheggio, transfer…): a differenza dei trattamenti
 * se ne possono aggiungere quanti se ne vuole, e una prenotazione può averne più d'uno.
 */
export function ServiziExtraStruttura({ strutturaId, puoScrivere }: { strutturaId: string; puoScrivere: boolean }) {
  const servizi = useServizi(strutturaId)
  // Schede nuove non ancora salvate, con una chiave locale per non perdere quello che si scrive.
  const [nuove, setNuove] = useState<number[]>([])
  const [prossima, setProssima] = useState(1)

  if (servizi.isLoading) return <Skeleton variant="rounded" height={120} />

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, maxWidth: 760 }}>
      <Box>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 17 }}>Servizi extra</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary, mt: 0.5 }}>
          Escursioni, parcheggio, transfer e tutto quello che vendi oltre al soggiorno. Nella prenotazione se ne possono aggiungere più
          d&apos;uno; il prezzo resta quello del momento in cui è stato aggiunto. Non vengono inviati all&apos;OTA.
        </Typography>
      </Box>

      {servizi.data?.map((s) => <SchedaServizio key={s.id} strutturaId={strutturaId} salvato={s} puoScrivere={puoScrivere} />)}
      {nuove.map((chiave) => (
        <SchedaServizio
          key={`nuovo-${chiave}`}
          strutturaId={strutturaId}
          salvato={null}
          puoScrivere={puoScrivere}
          onChiudi={() => setNuove((n) => n.filter((c) => c !== chiave))}
        />
      ))}

      {servizi.data?.length === 0 && nuove.length === 0 && (
        <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>Nessun servizio extra.</Typography>
      )}

      {puoScrivere && (
        <Box>
          <Button
            variant="outlined"
            size="small"
            onClick={() => {
              setNuove((n) => [...n, prossima])
              setProssima((p) => p + 1)
            }}
          >
            Aggiungi servizio
          </Button>
        </Box>
      )}
    </Box>
  )
}

function SchedaServizio({
  strutturaId,
  salvato,
  puoScrivere,
  onChiudi,
}: {
  strutturaId: string
  salvato: ServizioStrutturaDto | null
  puoScrivere: boolean
  /** Solo per una scheda nuova: la toglie dopo il salvataggio (compare nell'elenco) o se si rinuncia. */
  onChiudi?: () => void
}) {
  const mobile = useMobile()
  const toast = useToast()
  const crea = useCreaServizio(strutturaId)
  const aggiorna = useAggiornaServizio(strutturaId)
  const elimina = useEliminaServizio(strutturaId)
  const [nome, setNome] = useState(salvato?.nome ?? '')
  const [prezzo, setPrezzo] = useState(salvato ? String(salvato.prezzo) : '')
  const [modalita, setModalita] = useState<ModalitaPrezzoServizio>(salvato?.modalita ?? ModalitaPrezzoServizio.APersona)
  const [attivo, setAttivo] = useState(salvato?.attivo ?? true)
  const [iva, setIva] = useState(salvato ? codiceIva(salvato.aliquotaIva, salvato.natura) : '')
  const [confermaElimina, setConfermaElimina] = useState(false)
  const inCorso = crea.isPending || aggiorna.isPending || elimina.isPending
  const disabilitato = !puoScrivere || inCorso

  function gestisciErrore(err: unknown) {
    toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
  }

  function salva() {
    if (nome.trim() === '') {
      toast.errore('Indica il nome del servizio.')
      return
    }
    if (prezzo.trim() === '') {
      toast.errore(`Indica il prezzo di "${nome.trim()}".`)
      return
    }
    const richiesta = { nome: nome.trim(), prezzo: Number(prezzo), modalita, attivo, ...daCodiceIva(iva) }
    if (salvato) {
      aggiorna.mutate({ id: salvato.id, ...richiesta }, { onSuccess: () => toast.successo(`${richiesta.nome} salvato.`), onError: gestisciErrore })
    } else {
      crea.mutate(richiesta, {
        onSuccess: () => {
          toast.successo(`${richiesta.nome} aggiunto.`)
          onChiudi?.()
        },
        onError: gestisciErrore,
      })
    }
  }

  function eseguiElimina() {
    if (!salvato) return
    elimina.mutate(salvato.id, {
      onSuccess: () => {
        setConfermaElimina(false)
        toast.successo(`${salvato.nome} eliminato.`)
      },
      onError: (err) => {
        setConfermaElimina(false)
        gestisciErrore(err)
      },
    })
  }

  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 2.5, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 1.5, alignItems: mobile ? 'stretch' : 'center' }}>
        <TextField
          label="Nome"
          size="small"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          disabled={disabilitato}
          placeholder="Es. Escursione in barca"
          slotProps={{ htmlInput: { maxLength: 100 } }}
          sx={{ flex: 2 }}
        />
        <TextField
          label="Prezzo (€)"
          type="number"
          size="small"
          value={prezzo}
          onChange={(e) => setPrezzo(e.target.value)}
          disabled={disabilitato}
          slotProps={{ htmlInput: { min: 0 } }}
          sx={{ flex: 1 }}
        />
        <TextField select label="Si conta" size="small" value={modalita} onChange={(e) => setModalita(Number(e.target.value) as ModalitaPrezzoServizio)} disabled={disabilitato} sx={{ flex: 1.3 }}>
          {MODALITA.map((m) => (
            <MenuItem key={m} value={m}>
              {NOME_MODALITA[m]}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      {/* L'aliquota dipende da com'è offerto il servizio (accessorio all'alloggio o autonomo): la decide il commercialista. */}
      <TextField
        select
        label="IVA in fattura"
        size="small"
        value={iva}
        onChange={(e) => setIva(e.target.value)}
        disabled={disabilitato}
        helperText="Es. SPA o parcheggio per soli ospiti: spesso 10% come l'alloggio; aperti anche agli esterni: 22%. Chiedi al commercialista."
        slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        sx={{ maxWidth: mobile ? undefined : 360 }}
      >
        <MenuItem value="">Come la struttura (aliquota predefinita)</MenuItem>
        {ALIQUOTE_POSITIVE.map((a) => (
          <MenuItem key={a} value={`a${a}`}>
            {a}%
          </MenuItem>
        ))}
        {NATURE.map((n) => (
          <MenuItem key={n} value={`n${n}`}>
            0% — {ETICHETTA_NATURA[n]}
          </MenuItem>
        ))}
      </TextField>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1, flexWrap: 'wrap' }}>
        <FormControlLabel control={<Checkbox checked={attivo} onChange={(e) => setAttivo(e.target.checked)} disabled={disabilitato} />} label="Offerto" />
        {puoScrivere && (
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            {salvato ? (
              <Tooltip title="Elimina">
                <span>
                  <IconButton size="small" aria-label={`Elimina ${salvato.nome}`} onClick={() => setConfermaElimina(true)} disabled={inCorso}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
            ) : (
              <Button size="small" onClick={onChiudi} disabled={inCorso}>
                Annulla
              </Button>
            )}
            <Button variant="outlined" size="small" onClick={salva} disabled={inCorso}>
              Salva
            </Button>
          </Box>
        )}
      </Box>

      {confermaElimina && salvato && (
        <ConfirmDialog
          titolo="Eliminare il servizio?"
          messaggio={`"${salvato.nome}" non si potrà più aggiungere alle prenotazioni. Quelle che lo hanno già lo tengono, con il prezzo con cui è stato venduto.`}
          inCorso={elimina.isPending}
          onConferma={eseguiElimina}
          onAnnulla={() => setConfermaElimina(false)}
        />
      )}
    </Box>
  )
}
