import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import Drawer from '@mui/material/Drawer'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/Close'
import DownloadIcon from '@mui/icons-material/FileDownloadOutlined'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdfOutlined'
import SendIcon from '@mui/icons-material/SendOutlined'
import { ApiError } from '../../api/client'
import {
  erroriScarto,
  ESITO_TRASMISSIONE,
  EsitoTrasmissioneSdi,
  nomeDocumento,
  scaricaPdfRicevutaSdi,
  scaricaRicevutaSdi,
  scaricaXmlTrasmissione,
  STATO_SDI,
  StatoSdiFattura,
  TipoEmissioneDocumento,
  useInviaSdi,
  useTrasmissioniFattura,
  type DatiFatturaDto,
  type RicevutaSdiFatturaDto,
  type TrasmissioneFatturaDto,
} from '../../api/fatturazione'
import { ETICHETTA_TIPO_RICEVUTA_SDI } from '../../api/superAdmin'
import { fontDisplay, fontMono, stileImporto, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { useMobile } from '../../lib/useMobile'
import { ConfirmDialog } from '../ConfirmDialog'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

function dataOra(utc: string | null): string | null {
  return utc ? formattatoreDataOra.format(new Date(utc)) : null
}

interface Props {
  strutturaId: string
  fattura: DatiFatturaDto
  /** Concesso dal Super Admin e acceso dalla struttura: le fatture partono dal gestionale. */
  canaleAttivo: boolean
  puoScrivere: boolean
  onClose: () => void
}

/**
 * Pannello a destra di una fattura: a che punto è con lo SdI, perché è stata scartata (codici,
 * descrizioni e suggerimenti dello SdI), lo storico dei file e l'invio. Le altre azioni restano
 * nella riga dell'elenco.
 */
export function PannelloSdiFattura({ strutturaId, fattura, canaleAttivo, puoScrivere, onClose }: Props) {
  const mobile = useMobile()
  const toast = useToast()
  const trasmissioni = useTrasmissioniFattura(strutturaId, fattura.tipoEmissione === TipoEmissioneDocumento.Ricevuta ? null : fattura.id)
  const invia = useInviaSdi(strutturaId)
  const [conferma, setConferma] = useState(false)

  const ricevuta = fattura.tipoEmissione === TipoEmissioneDocumento.Ricevuta
  const stato = fattura.statoSdi != null ? STATO_SDI[fattura.statoSdi] : null
  const ultima = trasmissioni.data?.find((t) => t.viaCanale && !t.prova)
  const inviabile =
    canaleAttivo && puoScrivere && !ricevuta && (fattura.statoSdi === StatoSdiFattura.DaInviare || fattura.statoSdi === StatoSdiFattura.Scartata)
  const errori = fattura.statoSdi === StatoSdiFattura.Scartata ? erroriScarto(fattura.notaSdi) : []

  async function confermaInvio() {
    try {
      const esito = await invia.mutateAsync(fattura.id)
      toast.successo(
        esito.prova
          ? 'Inviata allo SdI di collaudo: per il fisco la fattura resta da inviare.'
          : `Fattura inviata allo SdI (identificativo ${esito.identificativoSdi}). Si attende la ricevuta.`,
      )
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Invio non riuscito, riprova.')
    } finally {
      setConferma(false)
    }
  }

  return (
    <Drawer anchor="right" open onClose={onClose} slotProps={{ paper: { sx: { width: mobile ? '100%' : 460, bgcolor: tokens.paper } } }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, p: 2.5 }}>
        <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 1 }}>
          <Box>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 17 }}>{nomeDocumento(fattura)}</Typography>
            <Typography sx={{ color: tokens.textSecondary, fontSize: 13.5 }}>
              {formattatoreData.format(new Date(fattura.dataDocumento))}
              {fattura.clienteNome ? ` · ${fattura.clienteNome}` : ''}
            </Typography>
            <Typography sx={{ ...stileImporto, fontWeight: 700, fontSize: 15, mt: 0.5 }}>{formattatoreValuta.format(fattura.importoTotale)}</Typography>
          </Box>
          <IconButton size="small" onClick={onClose} aria-label="Chiudi">
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>

        <Divider />

        {ricevuta && <Alert severity="info">Le ricevute di locazione breve non vanno allo SdI.</Alert>}

        {!ricevuta && !stato && <Alert severity="info">Fattura emessa prima dell'invio dal gestionale: il suo stato presso lo SdI non è registrato qui.</Alert>}

        {stato && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>Stato SdI</Typography>
              <Chip size="small" label={stato.etichetta} color={stato.colore} variant={stato.colore === 'default' ? 'outlined' : 'filled'} />
            </Box>

            <Alert severity={gravita(fattura.statoSdi!)}>{testoStato(fattura, canaleAttivo, ultima)}</Alert>

            {errori.length > 0 && (
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>{errori.length === 1 ? 'Motivo dello scarto' : 'Motivi dello scarto'}</Typography>
                {errori.map((e, i) => (
                  <Box key={i} sx={{ border: `1px solid ${tokens.error600}33`, bgcolor: tokens.error100, borderRadius: 1.5, p: 1.25 }}>
                    <Typography sx={{ fontSize: 13.5 }}>
                      {e.codice && (
                        <Box component="span" sx={{ fontFamily: fontMono, fontWeight: 700, mr: 1 }}>
                          {e.codice}
                        </Box>
                      )}
                      {e.descrizione}
                    </Typography>
                    {e.suggerimento && <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mt: 0.5 }}>Cosa fare: {e.suggerimento}</Typography>}
                  </Box>
                ))}
              </Box>
            )}

            {fattura.statoSdi === StatoSdiFattura.Scartata && errori.length === 0 && fattura.notaSdi && <Alert severity="error">{fattura.notaSdi}</Alert>}

            <Dati
              voci={[
                ['Identificativo SdI', fattura.identificativoSdi],
                ['Inviata il', dataOra(fattura.inviataSdiAtUtc)],
                ['Esito il', dataOra(fattura.esitoSdiAtUtc)],
              ]}
            />

            {inviabile && (
              <Button variant="contained" color="primary" startIcon={<SendIcon />} onClick={() => setConferma(true)} disabled={invia.isPending}>
                {fattura.statoSdi === StatoSdiFattura.Scartata ? 'Invia di nuovo allo SdI' : 'Invia allo SdI'}
              </Button>
            )}
          </Box>
        )}

        {!ricevuta && (
          <>
            <Divider />
            <Typography sx={{ fontWeight: 700, fontSize: 13.5 }}>Storico dei file</Typography>
            {trasmissioni.isLoading && <Skeleton variant="rounded" height={80} />}
            {trasmissioni.data?.length === 0 && <Typography sx={{ color: tokens.textSecondary, fontSize: 13 }}>Nessun file generato finora.</Typography>}
            {trasmissioni.data?.map((t) => (
              <RigaTrasmissione key={t.id} strutturaId={strutturaId} fatturaId={fattura.id} trasmissione={t} />
            ))}
          </>
        )}
      </Box>

      {conferma && (
        <ConfirmDialog
          titolo="Inviare allo SdI?"
          messaggio={`${nomeDocumento(fattura)} sarà trasmessa all'Agenzia delle Entrate. Una volta consegnata non si può più modificare: si corregge solo con una nota di credito.`}
          testoConferma="Invia"
          pericoloso={false}
          inCorso={invia.isPending}
          onConferma={confermaInvio}
          onAnnulla={() => setConferma(false)}
        />
      )}
    </Drawer>
  )
}

function gravita(stato: StatoSdiFattura): 'info' | 'success' | 'warning' | 'error' {
  switch (stato) {
    case StatoSdiFattura.Consegnata:
      return 'success'
    case StatoSdiFattura.NonConsegnata:
      return 'warning'
    case StatoSdiFattura.Scartata:
      return 'error'
    default:
      return 'info'
  }
}

/** Cosa vuol dire lo stato e cosa fare adesso. L'ultimo tentativo dal canale conta se non è andato a buon fine. */
function testoStato(f: DatiFatturaDto, canaleAttivo: boolean, ultima: TrasmissioneFatturaDto | undefined): string {
  const spiegazione = STATO_SDI[f.statoSdi!].spiegazione
  if (f.statoSdi !== StatoSdiFattura.DaInviare) return spiegazione
  if (ultima?.esito === EsitoTrasmissioneSdi.Rifiutato) {
    return `L'ultimo invio non è stato accettato dallo SdI${ultima.nota ? `: ${ultima.nota}` : '.'} Puoi riprovare.`
  }
  if (ultima?.esito === EsitoTrasmissioneSdi.EsitoIgnoto) {
    return "L'ultimo invio non ha avuto risposta: il file potrebbe essere arrivato o no. Riprova: se era già arrivato, lo SdI scarta il doppione."
  }
  return canaleAttivo
    ? `${spiegazione} Controlla i dati e premi "Invia allo SdI".`
    : `${spiegazione} Scarica l'XML e caricalo sul portale Fatture e Corrispettivi dell'Agenzia delle Entrate.`
}

function Dati({ voci }: { voci: [string, string | null][] }) {
  const presenti = voci.filter(([, v]) => v)
  if (presenti.length === 0) return null
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'auto 1fr', columnGap: 2, rowGap: 0.5, fontSize: 13 }}>
      {presenti.map(([etichetta, valore]) => (
        <Box key={etichetta} sx={{ display: 'contents' }}>
          <Box sx={{ color: tokens.textSecondary }}>{etichetta}</Box>
          <Box sx={{ fontFamily: etichetta === 'Identificativo SdI' ? fontMono : undefined }}>{valore}</Box>
        </Box>
      ))}
    </Box>
  )
}

function RigaTrasmissione({ strutturaId, fatturaId, trasmissione: t }: { strutturaId: string; fatturaId: string; trasmissione: TrasmissioneFatturaDto }) {
  const toast = useToast()
  const esito = ESITO_TRASMISSIONE[t.esito]
  const etichetta = t.esito === EsitoTrasmissioneSdi.Generato ? (t.viaCanale ? 'Preparato' : 'Scaricato') : esito.etichetta

  async function scarica() {
    try {
      await scaricaXmlTrasmissione(strutturaId, fatturaId, t)
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    }
  }

  async function scaricaRicevuta(r: RicevutaSdiFatturaDto, formato: 'xml' | 'pdf') {
    try {
      await (formato === 'pdf' ? scaricaPdfRicevutaSdi : scaricaRicevutaSdi)(strutturaId, fatturaId, t.id, r)
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    }
  }

  return (
    <Box
      sx={{
        border: `1px solid ${tokens.surfaceBorder}`,
        borderRadius: 1.5,
        bgcolor: tokens.surface,
        p: 1.25,
        display: 'flex',
        flexDirection: 'column',
        gap: 0.5,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography sx={{ fontFamily: fontMono, fontSize: 12.5, flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.nomeFile}</Typography>
        {t.prova && <Chip size="small" label="Collaudo" variant="outlined" />}
        <Chip size="small" label={etichetta} color={esito.colore} variant={esito.colore === 'default' ? 'outlined' : 'filled'} />
        <Tooltip title="Scarica il file">
          <IconButton size="small" onClick={scarica}>
            <DownloadIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
      <Typography sx={{ fontSize: 12, color: tokens.textSecondary }}>
        {[
          `Creato ${dataOra(t.createdAtUtc)}`,
          t.inviataAtUtc ? `inviato ${dataOra(t.inviataAtUtc)}` : null,
          t.esitoAtUtc ? `esito ${dataOra(t.esitoAtUtc)}` : null,
          t.inviataDa ? `da ${t.inviataDa}` : null,
        ]
          .filter(Boolean)
          .join(' · ')}
      </Typography>
      {t.nota && <Typography sx={{ fontSize: 12.5 }}>{t.nota}</Typography>}
      {t.ricevute.map((r) => (
        <Box key={r.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, borderTop: `1px solid ${tokens.surfaceBorder}`, pt: 0.5 }}>
          <Typography sx={{ fontSize: 12.5, flex: 1, minWidth: 0 }}>
            {ETICHETTA_TIPO_RICEVUTA_SDI[r.tipo] ?? 'Messaggio SdI'}
            <Box component="span" sx={{ color: tokens.textSecondary }}>{` · ${dataOra(r.ricevutaAtUtc)}`}</Box>
          </Typography>
          <Tooltip title="Ricevuta in PDF">
            <IconButton size="small" onClick={() => scaricaRicevuta(r, 'pdf')}>
              <PictureAsPdfIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Ricevuta originale firmata (XML)">
            <IconButton size="small" onClick={() => scaricaRicevuta(r, 'xml')}>
              <DownloadIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      ))}
    </Box>
  )
}
