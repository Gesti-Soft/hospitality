import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import ListSubheader from '@mui/material/ListSubheader'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import DownloadIcon from '@mui/icons-material/FileDownloadOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { ApiError } from '../../api/client'
import {
  EsitoFileRicevutoSdi,
  ETICHETTA_ESITO_FILE_RICEVUTO,
  ETICHETTA_TIPO_RICEVUTA_SDI,
  scaricaFileRicevutoSdi,
  scaricaRicevutaSdi,
  useAttribuisciFileRicevuto,
  useCanaleSdi,
  useDashboardSuperAdmin,
  type FileRicevutoSdiDto,
  type RicevutaSdiDto,
} from '../../api/superAdmin'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'

const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

/**
 * Il canale SdI è uno per tutti i Clienti: qui finisce quello che non si è sistemato da solo. Le
 * fatture dei fornitori senza destinatario (o con una partita IVA di più Clienti) si attribuiscono a
 * mano; le ricevute orfane e le consegne del collaudo si guardano e si scaricano.
 */
export function SuperAdminCanaleSdiPage() {
  const { isSuperAdmin } = useStruttura()
  const toast = useToast()
  const canale = useCanaleSdi(isSuperAdmin)
  const [daAttribuire, setDaAttribuire] = useState<FileRicevutoSdiDto | null>(null)

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  async function scarica(fn: () => Promise<void>) {
    try {
      await fn()
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    }
  }

  const dati = canale.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, maxWidth: 1100 }}>
      {canale.isLoading && <Skeleton variant="rounded" height={260} />}
      {canale.isError && <Alert severity="error">Impossibile leggere lo stato del canale SdI.</Alert>}

      {dati && (
        <>
          <Riquadro titolo="Canale">
            {dati.collaudo && <Alert severity="warning">Il canale spedisce allo SdI di collaudo: le fatture inviate non hanno valore fiscale.</Alert>}
            <Box sx={{ display: 'grid', gridTemplateColumns: 'auto 1fr', columnGap: 3, rowGap: 1, fontSize: 13.5 }}>
              <Box sx={{ color: tokens.textSecondary }}>Codice destinatario</Box>
              <Box sx={{ fontFamily: fontMono, fontWeight: 700 }}>{dati.codiceDestinatario ?? 'Non ancora assegnato'}</Box>
              <Box sx={{ color: tokens.textSecondary }}>Trasmittente</Box>
              <Box sx={{ fontFamily: fontMono }}>{dati.codiceFiscaleTrasmittente ?? 'Non configurato'}</Box>
              <Box sx={{ color: tokens.textSecondary }}>Ambiente</Box>
              <Box>{dati.collaudo ? 'Collaudo' : 'Produzione'}</Box>
            </Box>
            <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
              Il codice destinatario è quello che i Clienti comunicano ai fornitori per ricevere le fatture nel gestionale.
            </Typography>
          </Riquadro>

          <Riquadro titolo={`Fatture in quarantena${dati.quarantena.length ? ` (${dati.quarantena.length})` : ''}`}>
            <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
              Fatture di fornitori arrivate al canale che non si sa a quale struttura dare: nessuna ha la partita IVA o il codice fiscale del destinatario,
              oppure ce l'hanno strutture di Clienti diversi. Finché restano qui nessun Cliente le vede.
            </Typography>
            {dati.quarantena.length === 0 && <Vuoto testo="Nessuna fattura in quarantena." />}
            {dati.quarantena.map((f) => (
              <RigaFile
                key={f.id}
                file={f}
                onScarica={() => scarica(() => scaricaFileRicevutoSdi(f))}
                onAttribuisci={f.esito === EsitoFileRicevutoSdi.NonLeggibile ? undefined : () => setDaAttribuire(f)}
              />
            ))}
          </Riquadro>

          <Riquadro titolo={`Ricevute non abbinate${dati.ricevuteNonAbbinate.length ? ` (${dati.ricevuteNonAbbinate.length})` : ''}`}>
            <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
              Messaggi dello SdI che non si agganciano a nessuna fattura spedita dal gestionale: da controllare a mano.
            </Typography>
            {dati.ricevuteNonAbbinate.length === 0 && <Vuoto testo="Nessuna ricevuta da controllare." />}
            {dati.ricevuteNonAbbinate.map((r) => (
              <RigaRicevuta key={r.id} ricevuta={r} onScarica={() => scarica(() => scaricaRicevutaSdi(r))} />
            ))}
          </Riquadro>

          <Riquadro titolo="Ricezioni di collaudo">
            <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
              Fatture inventate inviate dallo SdI di collaudo: non vanno a nessun Cliente, servono solo a verificare la ricezione.
            </Typography>
            {dati.ricezioniDiCollaudo.length === 0 && <Vuoto testo="Nessuna ricezione di collaudo." />}
            {dati.ricezioniDiCollaudo.map((f) => (
              <RigaFile key={f.id} file={f} onScarica={() => scarica(() => scaricaFileRicevutoSdi(f))} />
            ))}
          </Riquadro>
        </>
      )}

      {daAttribuire && <AttribuisciDialog file={daAttribuire} onClose={() => setDaAttribuire(null)} />}
    </Box>
  )
}

function Riquadro({ titolo, children }: { titolo: string; children: React.ReactNode }) {
  return (
    <Box
      sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, p: 3, display: 'flex', flexDirection: 'column', gap: 1.5 }}
    >
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>{titolo}</Typography>
      {children}
    </Box>
  )
}

function Vuoto({ testo }: { testo: string }) {
  return <Typography sx={{ fontSize: 13, color: tokens.textSecondary }}>{testo}</Typography>
}

function RigaFile({ file: f, onScarica, onAttribuisci }: { file: FileRicevutoSdiDto; onScarica: () => void; onAttribuisci?: () => void }) {
  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 240 }}>
        <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>{f.fornitore ?? 'Fornitore non letto'}</Typography>
        <Typography sx={{ fontSize: 12, color: tokens.textSecondary }}>
          {[
            formattatoreDataOra.format(new Date(f.ricevutoAtUtc)),
            `identificativo ${f.identificativoSdi}`,
            f.identificativoCessionario ? `destinatario ${f.identificativoCessionario}` : null,
          ]
            .filter(Boolean)
            .join(' · ')}
        </Typography>
        {f.messaggio && <Typography sx={{ fontSize: 12.5, mt: 0.5 }}>{f.messaggio}</Typography>}
      </Box>
      <Chip size="small" label={ETICHETTA_ESITO_FILE_RICEVUTO[f.esito]} variant="outlined" />
      <Tooltip title={`Scarica ${f.nomeFile}`}>
        <IconButton size="small" onClick={onScarica}>
          <DownloadIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      {onAttribuisci && (
        <Button size="small" variant="outlined" color="secondary" onClick={onAttribuisci}>
          Attribuisci
        </Button>
      )}
    </Box>
  )
}

function RigaRicevuta({ ricevuta: r, onScarica }: { ricevuta: RicevutaSdiDto; onScarica: () => void }) {
  return (
    <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 1.5, p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 240 }}>
        <Typography sx={{ fontSize: 13.5, fontWeight: 700 }}>{ETICHETTA_TIPO_RICEVUTA_SDI[r.tipo] ?? 'Messaggio SdI'}</Typography>
        <Typography sx={{ fontSize: 12, color: tokens.textSecondary }}>
          {[
            formattatoreDataOra.format(new Date(r.ricevutaAtUtc)),
            r.identificativoSdi ? `identificativo ${r.identificativoSdi}` : null,
            r.nomeFileFattura ? `file ${r.nomeFileFattura}` : null,
          ]
            .filter(Boolean)
            .join(' · ')}
        </Typography>
        {r.descrizione && <Typography sx={{ fontSize: 12.5, mt: 0.5 }}>{r.descrizione}</Typography>}
      </Box>
      {r.prova && <Chip size="small" label="Collaudo" variant="outlined" />}
      <Tooltip title="Scarica la ricevuta">
        <IconButton size="small" onClick={onScarica}>
          <DownloadIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </Box>
  )
}

/** Si sceglie la struttura fra tutte, raggruppate per Cliente. La fattura la vedrà tutto il Cliente. */
function AttribuisciDialog({ file, onClose }: { file: FileRicevutoSdiDto; onClose: () => void }) {
  const toast = useToast()
  const dashboard = useDashboardSuperAdmin(true)
  const attribuisci = useAttribuisciFileRicevuto()
  const [strutturaId, setStrutturaId] = useState('')
  const clienti = (dashboard.data?.clienti ?? []).filter((c) => c.strutture.length > 0).sort((a, b) => a.ragioneSociale.localeCompare(b.ragioneSociale))

  async function conferma() {
    try {
      await attribuisci.mutateAsync({ fileRicevutoId: file.id, strutturaId })
      toast.successo('Fattura attribuita: ora la vede il Cliente della struttura scelta.')
      onClose()
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Attribuisci la fattura</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Typography sx={{ fontSize: 13.5 }}>
          {file.fornitore ?? 'Fornitore non letto'}
          {file.identificativoCessionario ? `, destinatario ${file.identificativoCessionario}` : ''}. La fattura la vedranno tutte le strutture del Cliente
          scelto, in "Fatture ricevute". Controlla che sia davvero sua: una fattura data al Cliente sbagliato mostra i suoi acquisti a un altro.
        </Typography>
        <TextField select label="Struttura" value={strutturaId} onChange={(e) => setStrutturaId(e.target.value)} disabled={dashboard.isLoading}>
          {clienti.flatMap((c) => [
            <ListSubheader key={`c-${c.id}`}>
              {c.ragioneSociale}
              {c.partitaIva ? ` · ${c.partitaIva}` : ''}
            </ListSubheader>,
            ...c.strutture.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.nome}
                {!s.attivo ? ' (disattivata)' : ''}
              </MenuItem>
            )),
          ])}
        </TextField>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={attribuisci.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="secondary" onClick={() => void conferma()} disabled={!strutturaId || attribuisci.isPending}>
          Attribuisci
        </Button>
      </DialogActions>
    </Dialog>
  )
}
