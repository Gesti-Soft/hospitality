import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Switch from '@mui/material/Switch'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import ApartmentOutlinedIcon from '@mui/icons-material/ApartmentOutlined'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteForeverIcon from '@mui/icons-material/DeleteForeverOutlined'
import { ApiError } from '../../api/client'
import {
  GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA,
  giorniDaDisattivazione,
  useAggiornaServiziStruttura,
  useAggiornaUtente,
  useDashboardSuperAdmin,
  useEliminaStrutturaDefinitivamente,
  useImpostaAttivoCliente,
  useResettaPasswordUtente,
  useResettaDueFattoriUtente,
  useSbloccaAccessoUtente,
  type ClienteAdminDto,
  type StrutturaAdminDto,
  type UtenteAdminDto,
} from '../../api/superAdmin'
import { useAggiornaCliente, useCreaCliente } from '../../api/clienti'
import { useImpostaAttivoStruttura } from '../../api/strutture'
import { useCreaUtente } from '../../api/utenti'
import { useAggiornaWubookLicenzaSuperAdmin, useWubookLicenzaSuperAdmin, type WubookLicenzaDto } from '../../api/superAdminImpostazioni'
import { useAggiornaLicenzaStruttura, useLicenzaStruttura, type LicenzaStrutturaDto } from '../../api/licenzaStruttura'
import { useWubookEventiRicevuti, type WubookEventoRicevutoDto } from '../../api/integrazioni'
import { useStruttura } from '../../struttura/StrutturaContext'
import { fontDisplay, fontMono, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { KpiCard } from '../../components/KpiCard'
import { CampoData } from '../../components/CampoData'
import { formatoInputData, isoLocale, parsaInputData } from '../../lib/date'
import { useMobile } from '../../lib/useMobile'
import { AzioniCardElenco, BottoneNuovo, CardElenco, MessaggioVuotoElenco, RigaCardMeta, TestataCardElenco } from '../../components/CardElenco'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

interface EsitoServizio {
  attivo: boolean
  errore?: string | null
  dettaglio?: string
}

const SERVIZI: {
  chiave: keyof Pick<StrutturaAdminDto, 'wubookAbilitato' | 'alloggiatiWebAbilitato' | 'osservatorioAbilitato' | 'payTouristAbilitato'>
  etichetta: string
  // Nome dell'esito mostrato sotto lo switch — non sempre coincide con l'etichetta della
  // concessione (es. si concede "Alloggiati Web" ma l'esito è l'invio "Polizia di Stato").
  statoNome: string
  // Esito dell'integrazione lato Cliente (es. "Alloggiati Web" attivata nelle sue Impostazioni) —
  // mostrato sotto lo switch di concessione, non un'informazione separata: uno switch "concesso"
  // senza sapere se il Cliente lo ha poi davvero attivato dice poco da solo.
  stato: (s: StrutturaAdminDto) => EsitoServizio
}[] = [
  {
    chiave: 'wubookAbilitato',
    etichetta: 'Wubook',
    statoNome: 'Wubook',
    stato: (s) => ({ attivo: s.wubookAttivo, errore: s.wubookUltimoErrore }),
  },
  { chiave: 'alloggiatiWebAbilitato', etichetta: 'Alloggiati Web', statoNome: 'Polizia di Stato', stato: (s) => ({ attivo: s.poliziaStatoAttiva }) },
  { chiave: 'osservatorioAbilitato', etichetta: 'Osservatorio', statoNome: 'Osservatorio', stato: (s) => ({ attivo: s.osservatorioAttivo }) },
  { chiave: 'payTouristAbilitato', etichetta: 'PayTourist', statoNome: 'PayTourist', stato: (s) => ({ attivo: s.payTouristAttivo }) },
]

export function SuperAdminClientiPage() {
  const mobile = useMobile()
  const { isSuperAdmin } = useStruttura()
  const dashboard = useDashboardSuperAdmin(isSuperAdmin)

  // Solo l'Id, non l'oggetto Cliente intero: dopo ogni switch (Attiva/riattiva struttura, Servizi
  // concessi...) la dashboard viene invalidata e rifetchata — se il dialogo tenesse in state
  // l'oggetto Cliente "fotografato" al click, resterebbe con i vecchi valori finché non lo si
  // richiude e riapre (bug reale riscontrato: lo switch "Riattiva" sembrava non fare nulla).
  const [clienteStruttureAperto, setClienteStruttureAperto] = useState<string | null>(null)
  const [clienteInModifica, setClienteInModifica] = useState<ClienteAdminDto | null>(null)
  const [nuovoClienteAperto, setNuovoClienteAperto] = useState(false)
  const [strutturaDaEliminare, setStrutturaDaEliminare] = useState<{ struttura: StrutturaAdminDto; clienteRagioneSociale: string } | null>(null)
  const [mostraNonAttivi, setMostraNonAttivi] = useState(false)

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  if (dashboard.isLoading) {
    return <Skeleton variant="rounded" height={480} />
  }

  if (dashboard.isError) {
    return <Alert severity="error">Impossibile caricare la dashboard. Riprova.</Alert>
  }

  const clienti = dashboard.data?.clienti ?? []
  const utenti = dashboard.data?.utenti ?? []
  const clienteConStruttureAperte = clienti.find((c) => c.id === clienteStruttureAperto) ?? null

  // Il titolare è l'utente con isClienteAccount=true (accesso libero a tutte le Strutture del
  // Cliente, senza bisogno di assegnazioni) — un Cliente può avere altri utenti (uno per struttura,
  // gestiti poi dal Cliente stesso in Utenti), qui interessa solo il titolare. Fallback sul più
  // vecchio se nessuno è ancora flaggato (non dovrebbe succedere dopo il backfill della migrazione,
  // rete di sicurezza per un Cliente creato prima di questa modifica).
  function trovaAdminCliente(clienteId: string): UtenteAdminDto | null {
    const utentiCliente = utenti.filter((u) => u.clienteId === clienteId)
    if (utentiCliente.length === 0) return null
    return (
      utentiCliente.find((u) => u.isClienteAccount) ??
      utentiCliente.reduce((piuVecchio, u) => (new Date(u.createdAtUtc) < new Date(piuVecchio.createdAtUtc) ? u : piuVecchio))
    )
  }

  const clientiAttivi = clienti.filter((c) => c.attivo).length
  const struttureTotali = clienti.reduce((tot, c) => tot + c.strutture.length, 0)
  const struttureConErroreLicenza = clienti.reduce(
    (tot, c) => tot + c.strutture.filter((s) => s.wubookAttivo && s.wubookUltimoErrore).length,
    0,
  )

  const struttureEliminabili = clienti.flatMap((c) =>
    c.strutture
      .filter((s) => !s.attivo && s.disattivataAtUtc && giorniDaDisattivazione(s.disattivataAtUtc) >= GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA)
      .map((s) => ({ struttura: s, clienteRagioneSociale: c.ragioneSociale })),
  )

  const clientiVisibili = mostraNonAttivi ? clienti : clienti.filter((c) => c.attivo)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3.5 }}>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 2 }}>
        <KpiCard etichetta="Clienti" valore={String(clienti.length)} dettaglio={`${clientiAttivi} attivi`} />
        <KpiCard etichetta="Strutture" valore={String(struttureTotali)} />
        <KpiCard
          etichetta="Errori Wubook"
          valore={String(struttureConErroreLicenza)}
          dettaglio="strutture da controllare"
          accento={struttureConErroreLicenza > 0}
          coloreAccento="error"
        />
      </Box>

      {struttureEliminabili.length > 0 && (
        <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.error600}`, borderRadius: 2, overflow: 'hidden' }}>
          <Box sx={{ p: '18px 20px', borderBottom: `1px solid ${tokens.surfaceBorder}` }}>
            <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Strutture eliminabili</Typography>
            <Typography sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              Disattivate da almeno {GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA} giorni — l'eliminazione è definitiva e cancella tutti i dati collegati
              (camere, prenotazioni, ospiti, fatture...). Nessuna cancellazione automatica: va confermata singolarmente.
            </Typography>
          </Box>
          {mobile && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, p: 1.5 }}>
              {struttureEliminabili.map(({ struttura, clienteRagioneSociale }) => (
                <CardElenco key={struttura.id}>
                  <TestataCardElenco titolo={struttura.nome} sottotitolo={clienteRagioneSociale} />
                  <RigaCardMeta
                    voci={[
                      {
                        etichetta: 'Disattivata il',
                        valore: struttura.disattivataAtUtc ? formattatoreData.format(new Date(struttura.disattivataAtUtc)) : '—',
                      },
                      {
                        etichetta: 'Giorni',
                        valore: struttura.disattivataAtUtc ? giorniDaDisattivazione(struttura.disattivataAtUtc) : '—',
                      },
                    ]}
                  />
                  <AzioniCardElenco>
                    <Tooltip title="Elimina definitivamente">
                      <IconButton size="small" color="error" onClick={() => setStrutturaDaEliminare({ struttura, clienteRagioneSociale })}>
                        <DeleteForeverIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </AzioniCardElenco>
                </CardElenco>
              ))}
            </Box>
          )}

          {!mobile && (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Cliente</TableCell>
                    <TableCell>Struttura</TableCell>
                    <TableCell>Disattivata il</TableCell>
                    <TableCell align="right">Giorni</TableCell>
                    <TableCell align="right">Azioni</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {struttureEliminabili.map(({ struttura, clienteRagioneSociale }) => (
                    <TableRow key={struttura.id} hover>
                      <TableCell sx={{ color: tokens.textSecondary }}>{clienteRagioneSociale}</TableCell>
                      <TableCell sx={{ fontWeight: 600 }}>{struttura.nome}</TableCell>
                      <TableCell sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
                        {struttura.disattivataAtUtc ? formattatoreData.format(new Date(struttura.disattivataAtUtc)) : '—'}
                      </TableCell>
                      <TableCell align="right" sx={{ fontFamily: fontMono }}>
                        {struttura.disattivataAtUtc ? giorniDaDisattivazione(struttura.disattivataAtUtc) : '—'}
                      </TableCell>
                      <TableCell align="right">
                        <Tooltip title="Elimina definitivamente">
                          <IconButton size="small" color="error" onClick={() => setStrutturaDaEliminare({ struttura, clienteRagioneSociale })}>
                            <DeleteForeverIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </Box>
      )}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1.5 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5 }}>Clienti</Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <FormControlLabel
              control={<Switch size="small" checked={mostraNonAttivi} onChange={(e) => setMostraNonAttivi(e.target.checked)} />}
              label={<Typography sx={{ fontSize: 12.5, fontWeight: 600, color: tokens.textSecondary }}>Mostra non attivi</Typography>}
            />
            <BottoneNuovo etichetta="+ Nuovo Cliente" onClick={() => setNuovoClienteAperto(true)} />
          </Box>
        </Box>

        {clientiVisibili.length === 0 ? (
          <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 4, textAlign: 'center', color: tokens.textSecondary }}>
            {clienti.length === 0 ? 'Nessun Cliente presente.' : 'Nessun Cliente attivo.'}
          </Box>
        ) : (
          <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 2 }}>
            {clientiVisibili.map((c) => (
              <ClienteCard
                key={c.id}
                cliente={c}
                adminUtente={trovaAdminCliente(c.id)}
                onVediStrutture={() => setClienteStruttureAperto(c.id)}
                onModifica={() => setClienteInModifica(c)}
              />
            ))}
          </Box>
        )}
      </Box>

      {clienteConStruttureAperte && (
        <StruttureClienteDialog cliente={clienteConStruttureAperte} onClose={() => setClienteStruttureAperto(null)} />
      )}
      {clienteInModifica && (
        <ModificaClienteDialog
          cliente={clienteInModifica}
          adminUtente={trovaAdminCliente(clienteInModifica.id)}
          onClose={() => setClienteInModifica(null)}
        />
      )}
      {nuovoClienteAperto && <NuovoClienteDialog onClose={() => setNuovoClienteAperto(false)} />}
      {strutturaDaEliminare && (
        <EliminaStrutturaDialog
          struttura={strutturaDaEliminare.struttura}
          clienteRagioneSociale={strutturaDaEliminare.clienteRagioneSociale}
          onClose={() => setStrutturaDaEliminare(null)}
        />
      )}
    </Box>
  )
}

function RigaStruttura({ struttura, clienteQuotaAnnua }: { struttura: StrutturaAdminDto; clienteQuotaAnnua: number | null }) {
  const aggiornaServizi = useAggiornaServiziStruttura()
  const impostaAttivo = useImpostaAttivoStruttura()
  const queryClient = useQueryClient()
  const toast = useToast()

  function cambiaServizio(chiave: (typeof SERVIZI)[number]['chiave'], valore: boolean) {
    aggiornaServizi.mutate(
      {
        strutturaId: struttura.id,
        request: {
          wubookAbilitato: struttura.wubookAbilitato,
          alloggiatiWebAbilitato: struttura.alloggiatiWebAbilitato,
          osservatorioAbilitato: struttura.osservatorioAbilitato,
          payTouristAbilitato: struttura.payTouristAbilitato,
          [chiave]: valore,
        },
      },
      { onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  function cambiaAttivo(attivo: boolean) {
    impostaAttivo.mutate(
      { strutturaId: struttura.id, attivo },
      {
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] }),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.'),
      },
    )
  }

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        gap: 1.25,
        p: '10px 14px',
        border: `1px solid ${struttura.attivo ? tokens.surfaceBorder : tokens.error600}`,
        borderRadius: 1.5,
        bgcolor: tokens.surface,
        opacity: struttura.attivo ? 1 : 0.7,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
        <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 160 }}>{struttura.nome}</Typography>

        <Tooltip title={struttura.attivo ? 'Elimina struttura (soft-delete)' : 'Riattiva struttura'}>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, minWidth: 90 }}>
            <Switch size="small" checked={struttura.attivo} onChange={(e) => cambiaAttivo(e.target.checked)} disabled={impostaAttivo.isPending} />
            <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: struttura.attivo ? tokens.ok600 : tokens.error600 }}>
              {struttura.attivo ? 'Attiva' : 'Eliminata'}
            </Typography>
          </Box>
        </Tooltip>

        {/* Licenza software GestiSoft (non Wubook) — subito accanto al nome, non sotto un servizio
            specifico: riguarda l'intera struttura, non un'integrazione in particolare. */}
        {struttura.scadenzaLicenza && new Date(struttura.scadenzaLicenza) < new Date() && (
          <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: tokens.error600 }}>
            Scaduto il {formattatoreData.format(new Date(struttura.scadenzaLicenza))}
          </Typography>
        )}
      </Box>

      <Box>
        <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em', mb: 0.75 }}>
          Servizi concessi
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 2.5, flexWrap: 'wrap' }}>
          {SERVIZI.map((s) => {
            const esito = s.stato(struttura)
            return (
              <Box key={s.chiave} sx={{ display: 'flex', flexDirection: 'column', gap: 0.75, minWidth: 170 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                  <Switch
                    size="small"
                    checked={struttura[s.chiave]}
                    onChange={(e) => cambiaServizio(s.chiave, e.target.checked)}
                    disabled={aggiornaServizi.isPending}
                  />
                  <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{s.etichetta}</Typography>
                </Box>
                <EsitoServizioBadge nome={s.statoNome} {...esito} />
              </Box>
            )
          })}
        </Box>
      </Box>

      <LicenzaStrutturaRiga strutturaId={struttura.id} clienteQuotaAnnua={clienteQuotaAnnua} />
      {struttura.wubookAbilitato && <ConfigWubookRiga strutturaId={struttura.id} />}
    </Box>
  )
}

/**
 * Licenza software GestiSoft della Struttura (scadenza + rinnovo) — SEMPRE visibile, indipendente da
 * quali integrazioni esterne siano concesse: NON è la licenza Wubook (quella è solo il Codice
 * struttura/lcode, vedi ConfigWubookRiga sotto).
 */
function LicenzaStrutturaRiga({ strutturaId, clienteQuotaAnnua }: { strutturaId: string; clienteQuotaAnnua: number | null }) {
  const licenza = useLicenzaStruttura(strutturaId)
  const aggiorna = useAggiornaLicenzaStruttura(strutturaId)
  const toast = useToast()

  const [scadenzaLicenza, setScadenzaLicenza] = useState('')
  const [scadenzaOriginale, setScadenzaOriginale] = useState('')
  const [rinnovoDaConfermare, setRinnovoDaConfermare] = useState(false)

  useEffect(() => {
    if (licenza.data) {
      caricaDati(licenza.data)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [licenza.data])

  function caricaDati(dati: LicenzaStrutturaDto) {
    const scadenza = dati.scadenzaLicenza ? formatoInputData(new Date(dati.scadenzaLicenza)) : ''
    setScadenzaLicenza(scadenza)
    setScadenzaOriginale(scadenza)
  }

  function estendi(unita: 'giorni' | 'mesi', quantita: number) {
    const base = scadenzaLicenza ? parsaInputData(scadenzaLicenza) : new Date()
    const partenza = base.getTime() > Date.now() ? base : new Date()
    const nuova = new Date(partenza)
    if (unita === 'giorni') {
      nuova.setDate(nuova.getDate() + quantita)
    } else {
      nuova.setMonth(nuova.getMonth() + quantita)
    }
    setScadenzaLicenza(formatoInputData(nuova))
  }

  function salva() {
    // La domanda "è un rinnovo pagato?" ha senso solo quando la scadenza sta davvero cambiando e
    // solo se si sta impostando una nuova data, non svuotandola.
    if (scadenzaLicenza !== scadenzaOriginale && scadenzaLicenza !== '') {
      setRinnovoDaConfermare(true)
      return
    }
    salvaEffettiva(false, null)
  }

  function salvaEffettiva(rinnovoPagato: boolean, importoRinnovo: number | null) {
    aggiorna.mutate(
      {
        scadenzaLicenza: scadenzaLicenza ? isoLocale(parsaInputData(scadenzaLicenza)) : null,
        rinnovoPagato,
        importoRinnovo,
      },
      {
        onSuccess: (dati) => {
          caricaDati(dati)
          toast.successo(rinnovoPagato ? 'Licenza salvata, rinnovo registrato.' : 'Licenza salvata.')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  if (licenza.isLoading) {
    return <Skeleton variant="rounded" height={64} />
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, pt: 1, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
      <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
        Licenza GestiSoft
      </Typography>

      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
        <CampoData label="Scadenza" value={scadenzaLicenza} onChange={setScadenzaLicenza} size="small" disabled={aggiorna.isPending} />
        <Box sx={{ display: 'flex', gap: 0.75, alignItems: 'center' }}>
          <Button size="small" variant="outlined" onClick={() => estendi('mesi', 1)} disabled={aggiorna.isPending}>
            +1 mese
          </Button>
          <Button size="small" variant="outlined" onClick={() => estendi('mesi', 6)} disabled={aggiorna.isPending}>
            +6 mesi
          </Button>
          <Button size="small" variant="outlined" onClick={() => estendi('mesi', 12)} disabled={aggiorna.isPending}>
            +1 anno
          </Button>
          <Button size="small" variant="outlined" onClick={() => estendi('giorni', 30)} disabled={aggiorna.isPending}>
            +30 giorni
          </Button>
        </Box>
        <Button size="small" variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
          Salva licenza
        </Button>
      </Box>

      {rinnovoDaConfermare && (
        <RinnovoLicenzaDialog
          nuovaScadenza={scadenzaLicenza}
          suggerimentoImporto={clienteQuotaAnnua}
          inCorso={aggiorna.isPending}
          onNonPagato={() => {
            setRinnovoDaConfermare(false)
            salvaEffettiva(false, null)
          }}
          onPagato={(importo) => {
            setRinnovoDaConfermare(false)
            salvaEffettiva(true, importo)
          }}
          onAnnulla={() => setRinnovoDaConfermare(false)}
        />
      )}
    </Box>
  )
}

/** Credenziali Wubook della Struttura (codice struttura/lcode + utente-token gestisoft.it per il polling eventi) — solo se Wubook è concesso. */
function ConfigWubookRiga({ strutturaId }: { strutturaId: string }) {
  const licenza = useWubookLicenzaSuperAdmin(strutturaId)
  const aggiorna = useAggiornaWubookLicenzaSuperAdmin(strutturaId)
  const toast = useToast()

  const [gestisoftUsername, setGestisoftUsername] = useState('')
  const [gestisoftToken, setGestisoftToken] = useState('')
  const [codiceStruttura, setCodiceStruttura] = useState('')
  const [prenotazioniRicevuteAperto, setPrenotazioniRicevuteAperto] = useState(false)

  useEffect(() => {
    if (licenza.data) {
      caricaDati(licenza.data)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [licenza.data])

  function caricaDati(dati: WubookLicenzaDto) {
    setGestisoftUsername(dati.gestisoftUsername ?? '')
    setGestisoftToken(dati.gestisoftToken ?? '')
    setCodiceStruttura(dati.codiceStruttura ?? '')
  }

  function salva() {
    aggiorna.mutate(
      {
        gestisoftUsername: gestisoftUsername.trim() || null,
        gestisoftToken: gestisoftToken.trim() || null,
        codiceStruttura: codiceStruttura.trim() || null,
      },
      {
        onSuccess: (dati) => {
          caricaDati(dati)
          toast.successo('Configurazione Wubook salvata.')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  if (licenza.isLoading) {
    return <Skeleton variant="rounded" height={140} />
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, pt: 1, borderTop: `1px solid ${tokens.surfaceBorder}` }}>
      <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
        Wubook
      </Typography>

      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          label="Codice struttura (lcode)"
          value={codiceStruttura}
          onChange={(e) => setCodiceStruttura(e.target.value)}
          disabled={aggiorna.isPending}
          sx={{ minWidth: 200 }}
        />
      </Box>

      <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em', mt: 0.5 }}>
        Solo per l'intercettazione prenotazioni (gestisoft.it)
      </Typography>
      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          label="Utente gestisoft.it"
          value={gestisoftUsername}
          onChange={(e) => setGestisoftUsername(e.target.value)}
          disabled={aggiorna.isPending}
          sx={{ minWidth: 200 }}
        />
        <TextField
          size="small"
          label="Token gestisoft.it"
          value={gestisoftToken}
          onChange={(e) => setGestisoftToken(e.target.value)}
          disabled={aggiorna.isPending}
          sx={{ minWidth: 200 }}
        />
      </Box>

      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button size="small" variant="contained" color="primary" onClick={salva} disabled={aggiorna.isPending}>
          Salva Wubook
        </Button>
        <Button size="small" variant="outlined" onClick={() => setPrenotazioniRicevuteAperto(true)}>
          Prenotazioni ricevute
        </Button>
      </Box>

      {prenotazioniRicevuteAperto && <PrenotazioniRicevuteDialog strutturaId={strutturaId} onClose={() => setPrenotazioniRicevuteAperto(false)} />}
    </Box>
  )
}

function RinnovoLicenzaDialog({
  nuovaScadenza,
  suggerimentoImporto,
  inCorso,
  onNonPagato,
  onPagato,
  onAnnulla,
}: {
  nuovaScadenza: string
  suggerimentoImporto: number | null
  inCorso: boolean
  onNonPagato: () => void
  onPagato: (importo: number | null) => void
  onAnnulla: () => void
}) {
  const [chiestoImporto, setChiestoImporto] = useState(false)
  const [importo, setImporto] = useState(suggerimentoImporto != null ? String(suggerimentoImporto) : '')

  return (
    <Dialog open onClose={onAnnulla} maxWidth="xs" fullWidth>
      <DialogTitle>Rinnovo licenza</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Typography sx={{ fontSize: 13.5 }}>
          Stai impostando la scadenza al <strong>{formattatoreData.format(parsaInputData(nuovaScadenza))}</strong>. È un rinnovo pagato dal Cliente?
        </Typography>
        {chiestoImporto && (
          <TextField
            label="Importo pagato (€)"
            value={importo}
            onChange={(e) => setImporto(e.target.value)}
            autoFocus
            fullWidth
            disabled={inCorso}
            helperText="Facoltativo — solo per il totale incassi in Statistiche"
          />
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onAnnulla} disabled={inCorso}>
          Annulla
        </Button>
        {!chiestoImporto && (
          <Button onClick={onNonPagato} disabled={inCorso}>
            No, non pagato
          </Button>
        )}
        <Button
          variant="contained"
          color="secondary"
          disabled={inCorso}
          onClick={() => {
            if (!chiestoImporto) {
              setChiestoImporto(true)
              return
            }
            onPagato(importo.trim() === '' ? null : Number(importo.replace(',', '.')))
          }}
        >
          {chiestoImporto ? 'Registra e salva' : 'Sì, pagato'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}

type FiltroLetta = 'tutte' | 'lette' | 'non-lette'

function PrenotazioniRicevuteDialog({ strutturaId, onClose }: { strutturaId: string; onClose: () => void }) {
  const mobile = useMobile()
  const eventi = useWubookEventiRicevuti(strutturaId, true)
  const [filtro, setFiltro] = useState<FiltroLetta>('tutte')

  const eventiFiltrati = (eventi.data ?? []).filter((e) => {
    if (filtro === 'lette') return e.importazioneRiuscita
    if (filtro === 'non-lette') return !e.importazioneRiuscita
    return true
  })

  const messaggioVuoto = (eventi.data ?? []).length === 0 ? 'Nessuna prenotazione ricevuta finora.' : 'Nessuna prenotazione per questo filtro.'

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>Prenotazioni Wubook ricevute</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: 1 }}>
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Ogni prenotazione Wubook intercettata (tramite gestisoft.it) — "Non letta" vuol dire che l'importazione non è ancora riuscita e
          verrà ritentata: in caso di problemi, Lcode e Rcode bastano per recuperarla a mano da Wubook. Ultime{' '}
          {Math.min(eventi.data?.length ?? 0, 200)} al massimo, più recenti prima.
        </Typography>

        <ToggleButtonGroup exclusive size="small" value={filtro} onChange={(_, v) => v && setFiltro(v)}>
          <ToggleButton value="tutte">Tutte</ToggleButton>
          <ToggleButton value="lette">Lette</ToggleButton>
          <ToggleButton value="non-lette">Non lette</ToggleButton>
        </ToggleButtonGroup>

        {eventi.isLoading && <Skeleton variant="rounded" height={220} />}
        {eventi.isError && <Alert severity="error">Impossibile caricare le prenotazioni ricevute. Riprova.</Alert>}

        {!eventi.isLoading && !eventi.isError && mobile && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            {eventiFiltrati.length === 0 && <MessaggioVuotoElenco messaggio={messaggioVuoto} />}
            {eventiFiltrati.map((e) => (
              <CardEventoRicevuto key={e.id} evento={e} />
            ))}
          </Box>
        )}

        {!eventi.isLoading && !eventi.isError && !mobile && (
          <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, overflow: 'hidden' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Data</TableCell>
                  <TableCell>Lcode</TableCell>
                  <TableCell>Rcode</TableCell>
                  <TableCell>Esito</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {eventiFiltrati.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                      {messaggioVuoto}
                    </TableCell>
                  </TableRow>
                )}
                {eventiFiltrati.map((e) => (
                  <RigaEventoRicevuto key={e.id} evento={e} />
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose}>Chiudi</Button>
      </DialogActions>
    </Dialog>
  )
}

function RigaEventoRicevuto({ evento }: { evento: WubookEventoRicevutoDto }) {
  const data = evento.updatedAtUtc ?? evento.createdAtUtc
  const riga = (
    <TableRow hover>
      <TableCell sx={{ fontFamily: fontMono, fontSize: 12 }}>{formattatoreDataOra.format(new Date(data))}</TableCell>
      <TableCell sx={{ fontFamily: fontMono, fontSize: 12 }}>{evento.lcode}</TableCell>
      <TableCell sx={{ fontFamily: fontMono, fontSize: 12 }}>{evento.rcode}</TableCell>
      <TableCell>
        <Chip
          size="small"
          label={evento.importazioneRiuscita ? 'Letta' : 'Non letta'}
          sx={{ bgcolor: evento.importazioneRiuscita ? tokens.ok600 : tokens.error600, color: '#fff', fontWeight: 700 }}
        />
      </TableCell>
    </TableRow>
  )

  return evento.messaggioErrore ? <Tooltip title={evento.messaggioErrore}>{riga}</Tooltip> : riga
}

function CardEventoRicevuto({ evento }: { evento: WubookEventoRicevutoDto }) {
  const data = evento.updatedAtUtc ?? evento.createdAtUtc
  const esito = (
    <Chip
      size="small"
      label={evento.importazioneRiuscita ? 'Letta' : 'Non letta'}
      sx={{ bgcolor: evento.importazioneRiuscita ? tokens.ok600 : tokens.error600, color: '#fff', fontWeight: 700 }}
    />
  )

  return (
    <CardElenco>
      <TestataCardElenco
        titolo={formattatoreDataOra.format(new Date(data))}
        azioneDestra={evento.messaggioErrore ? <Tooltip title={evento.messaggioErrore}>{esito}</Tooltip> : esito}
      />
      <RigaCardMeta
        voci={[
          { etichetta: 'Lcode', valore: evento.lcode },
          { etichetta: 'Rcode', valore: evento.rcode },
        ]}
      />
    </CardElenco>
  )
}

function EsitoServizioBadge({ nome, attivo, errore, dettaglio }: EsitoServizio & { nome: string }) {
  const inErrore = attivo && !!errore
  const colore = inErrore ? tokens.error600 : attivo ? tokens.ok600 : tokens.textTertiary

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, pl: '2px' }}>
      <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: colore, flex: '0 0 auto' }} />
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: 11.5, fontWeight: 600, color: tokens.textPrimary }}>{nome}</Typography>
        <Typography noWrap sx={{ fontSize: 11, color: inErrore ? tokens.error600 : tokens.textTertiary }}>
          {inErrore ? errore : attivo ? (dettaglio ?? 'attivo') : 'non attivo'}
        </Typography>
      </Box>
    </Box>
  )
}

function ClienteCard({
  cliente,
  adminUtente,
  onVediStrutture,
  onModifica,
}: {
  cliente: ClienteAdminDto
  adminUtente: UtenteAdminDto | null
  onVediStrutture: () => void
  onModifica: () => void
}) {
  const impostaAttivoCliente = useImpostaAttivoCliente()
  const toast = useToast()

  function cambiaAttivo(attivo: boolean) {
    impostaAttivoCliente.mutate(
      { clienteId: cliente.id, attivo },
      { onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita.') },
    )
  }

  return (
    <Box
      sx={{
        border: `1px solid ${tokens.surfaceBorder}`,
        borderRadius: 2,
        bgcolor: tokens.surface,
        p: 2.25,
        display: 'flex',
        flexDirection: 'column',
        gap: 1.5,
        minWidth: 0,
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 1 }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15, wordBreak: 'break-word' }}>{cliente.ragioneSociale}</Typography>
          {adminUtente && (adminUtente.nome || adminUtente.cognome) && (
            <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              {[adminUtente.nome, adminUtente.cognome].filter(Boolean).join(' ')}
            </Typography>
          )}
          <Typography sx={{ fontSize: 12, color: tokens.textSecondary, fontFamily: fontMono, mt: 0.25 }}>
            {cliente.partitaIva ?? 'P.IVA non indicata'}
          </Typography>
          {adminUtente && (
            <Typography noWrap sx={{ fontSize: 12, color: tokens.textSecondary, mt: 0.25 }}>
              {adminUtente.email}
            </Typography>
          )}
        </Box>
        <Box sx={{ display: 'flex', gap: 0.25, flex: '0 0 auto' }}>
          <Tooltip title="Strutture del Cliente">
            <IconButton size="small" onClick={onVediStrutture} disabled={cliente.strutture.length === 0}>
              <ApartmentOutlinedIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Modifica dati Cliente e amministratore">
            <IconButton size="small" onClick={onModifica}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1.5 }}>
        <Tooltip title={cliente.attivo ? 'Sospendi il Cliente (nessun suo utente potrà più accedere)' : 'Riattiva il Cliente'}>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
            <Switch
              size="small"
              checked={cliente.attivo}
              onChange={(e) => cambiaAttivo(e.target.checked)}
              disabled={impostaAttivoCliente.isPending}
            />
            <Typography sx={{ fontSize: 11.5, fontWeight: 700, color: cliente.attivo ? tokens.ok600 : tokens.error600 }}>
              {cliente.attivo ? 'Attivo' : 'Sospeso'}
            </Typography>
          </Box>
        </Tooltip>
        <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
          Creato il {formattatoreData.format(new Date(cliente.createdAtUtc))}
        </Typography>
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1.5 }}>
        <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.textSecondary }}>
          {cliente.strutture.length === 0 ? 'Nessuna struttura' : `${cliente.strutture.length} ${cliente.strutture.length === 1 ? 'struttura' : 'strutture'}`}
        </Typography>
        {cliente.quotaAnnua != null && (
          <Typography sx={{ fontSize: 12.5, fontWeight: 700, color: tokens.ok600 }}>{formattatoreValuta.format(cliente.quotaAnnua)}/anno</Typography>
        )}
        {cliente.note && (
          <Tooltip title={cliente.note}>
            <Typography noWrap sx={{ fontSize: 12, color: tokens.textTertiary, fontStyle: 'italic', maxWidth: 160 }}>
              "{cliente.note}"
            </Typography>
          </Tooltip>
        )}
      </Box>
    </Box>
  )
}

function StruttureClienteDialog({ cliente, onClose }: { cliente: ClienteAdminDto; onClose: () => void }) {
  const [strutturaId, setStrutturaId] = useState(cliente.strutture[0]?.id ?? '')
  const struttura = cliente.strutture.find((s) => s.id === strutturaId) ?? null
  const [daEliminare, setDaEliminare] = useState(false)

  // Stessa soglia della tabella "Strutture eliminabili" in dashboard — qui è comodo poterla
  // eliminare subito dalla struttura che si sta già guardando, senza dover tornare indietro e
  // ricercarla in quella tabella.
  const eliminabile =
    !!struttura && !struttura.attivo && !!struttura.disattivataAtUtc && giorniDaDisattivazione(struttura.disattivataAtUtc) >= GIORNI_MINIMI_ELIMINAZIONE_STRUTTURA

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Strutture di {cliente.ragioneSociale}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {/* Il primo figlio letterale di questo contenitore flex non deve mai essere il campo con
            floating label sottostante: senza qualcosa che lo preceda, MUI ne taglia a metà la label
            (bug di rendering noto, vedi altri dialog dell'app con lo stesso pattern). */}
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>Seleziona la struttura da gestire.</Typography>
        <TextField select label="Struttura" value={strutturaId} onChange={(e) => setStrutturaId(e.target.value)} fullWidth>
          {cliente.strutture.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.nome}
            </MenuItem>
          ))}
        </TextField>
        {struttura && <RigaStruttura struttura={struttura} clienteQuotaAnnua={cliente.quotaAnnua} />}
        {eliminabile && (
          <Alert
            severity="warning"
            action={
              <Button color="error" size="small" onClick={() => setDaEliminare(true)}>
                Elimina definitivamente
              </Button>
            }
          >
            Disattivata da {giorniDaDisattivazione(struttura!.disattivataAtUtc!)} giorni: può essere eliminata in modo definitivo.
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose}>Chiudi</Button>
      </DialogActions>

      {daEliminare && struttura && (
        <EliminaStrutturaDialog
          struttura={struttura}
          clienteRagioneSociale={cliente.ragioneSociale}
          onClose={() => setDaEliminare(false)}
          onEliminata={() => {
            setDaEliminare(false)
            // La struttura appena eliminata non esiste più: l'elenco di questo dialogo (cliente.strutture)
            // è uno snapshot passato dal genitore e non si aggiorna da solo — si chiude tutto, il
            // genitore ha già invalidato la query e mostrerà i dati aggiornati alla riapertura.
            onClose()
          }}
        />
      )}
    </Dialog>
  )
}

function ModificaClienteDialog({
  cliente,
  adminUtente,
  onClose,
}: {
  cliente: ClienteAdminDto
  adminUtente: UtenteAdminDto | null
  onClose: () => void
}) {
  const mobile = useMobile()
  const [ragioneSociale, setRagioneSociale] = useState(cliente.ragioneSociale)
  const [partitaIva, setPartitaIva] = useState(cliente.partitaIva ?? '')
  const [quotaAnnua, setQuotaAnnua] = useState(cliente.quotaAnnua != null ? String(cliente.quotaAnnua) : '')
  const [note, setNote] = useState(cliente.note ?? '')
  const [email, setEmail] = useState(adminUtente?.email ?? '')
  const [nome, setNome] = useState(adminUtente?.nome ?? '')
  const [cognome, setCognome] = useState(adminUtente?.cognome ?? '')
  const [isClienteAccount, setIsClienteAccount] = useState(adminUtente?.isClienteAccount ?? true)
  const [errore, setErrore] = useState<string | null>(null)

  const [nuovaPassword, setNuovaPassword] = useState('')
  const toast = useToast()

  const aggiorna = useAggiornaCliente()
  const aggiornaUtente = useAggiornaUtente()
  const resetPassword = useResettaPasswordUtente()
  const reset2Fa = useResettaDueFattoriUtente()
  const sblocca = useSbloccaAccessoUtente()
  const [confermaReset2Fa, setConfermaReset2Fa] = useState(false)
  const queryClient = useQueryClient()

  const inCorso = aggiorna.isPending || aggiornaUtente.isPending

  async function salva() {
    if (ragioneSociale.trim() === '') {
      setErrore('La ragione sociale è obbligatoria.')
      return
    }
    if (adminUtente && email.trim() === '') {
      setErrore('Email obbligatoria.')
      return
    }
    setErrore(null)
    try {
      await aggiorna.mutateAsync({
        clienteId: cliente.id,
        request: {
          ragioneSociale: ragioneSociale.trim(),
          partitaIva: partitaIva.trim() || null,
          quotaAnnua: quotaAnnua.trim() === '' ? null : Number(quotaAnnua.replace(',', '.')),
          note: note.trim() || null,
        },
      })
      if (adminUtente) {
        await aggiornaUtente.mutateAsync({
          utenteId: adminUtente.id,
          request: { email: email.trim(), nome: nome.trim() || null, cognome: cognome.trim() || null, isClienteAccount },
        })
      }
      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
      onClose()
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    }
  }

  function reimpostaPassword() {
    if (!adminUtente) return
    if (nuovaPassword.trim().length < 8) {
      toast.errore('La password deve avere almeno 8 caratteri.')
      return
    }
    resetPassword.mutate(
      { utenteId: adminUtente.id, nuovaPassword: nuovaPassword.trim() },
      {
        onSuccess: () => {
          toast.successo('Password reimpostata.')
          setNuovaPassword('')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  function sbloccaAccesso() {
    if (!adminUtente) return
    sblocca.mutate(
      { utenteId: adminUtente.id },
      {
        onSuccess: () => toast.successo('Accesso sbloccato: può riprovare subito.'),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  function azzeraDueFattori() {
    if (!adminUtente) return
    reset2Fa.mutate(
      { utenteId: adminUtente.id },
      {
        onSuccess: () => {
          setConfermaReset2Fa(false)
          toast.successo('Verifica in due passaggi azzerata: ora entra con la sola password.')
        },
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>Modifica Cliente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>
        <TextField
          label="Ragione sociale"
          value={ragioneSociale}
          onChange={(e) => setRagioneSociale(e.target.value)}
          fullWidth
          disabled={inCorso}
          autoFocus
        />
        <TextField label="P.IVA" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={inCorso} />

        <Divider />
        <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
          Solo per il Super Admin
        </Typography>
        <TextField
          label="Quota annua (€)"
          value={quotaAnnua}
          onChange={(e) => setQuotaAnnua(e.target.value)}
          fullWidth
          disabled={inCorso}
          helperText="Solo un promemoria, non genera fatture"
        />
        <TextField
          label="Note"
          value={note}
          onChange={(e) => setNote(e.target.value)}
          fullWidth
          multiline
          minRows={2}
          disabled={inCorso}
          helperText="Appunti privati, mai visibili al Cliente"
        />

        {adminUtente ? (
          <>
            <Divider />
            <Typography sx={{ fontSize: 11, fontWeight: 700, color: tokens.textTertiary, textTransform: 'uppercase', letterSpacing: '.05em' }}>
              Utente amministratore
            </Typography>
            <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={inCorso} />
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
              <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
              <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
            </Box>
            <FormControlLabel
              control={<Switch checked={isClienteAccount} onChange={(e) => setIsClienteAccount(e.target.checked)} disabled={inCorso} />}
              label="Account Cliente (accesso libero a tutte le strutture, senza bisogno di assegnazioni)"
            />

            <Divider />
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Dopo 5 password sbagliate l'accesso si blocca per 15 minuti. Qui lo togli subito, senza cambiargli la
              password.
            </Typography>
            <Box>
              <Button variant="outlined" onClick={sbloccaAccesso} disabled={sblocca.isPending} sx={{ whiteSpace: 'nowrap' }}>
                Sblocca accesso
              </Button>
            </Box>

            <Divider />
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Reimposta la password di questo utente (non serve conoscere quella attuale). Toglie anche l'eventuale
              blocco per tentativi falliti.
            </Typography>
            <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2, alignItems: mobile ? 'stretch' : 'flex-start' }}>
              <TextField
                label="Nuova password"
                type="password"
                value={nuovaPassword}
                onChange={(e) => setNuovaPassword(e.target.value)}
                fullWidth
                disabled={resetPassword.isPending}
              />
              <Button
                variant="outlined"
                onClick={reimpostaPassword}
                disabled={resetPassword.isPending || nuovaPassword.trim() === ''}
                sx={{ whiteSpace: 'nowrap', flex: '0 0 auto' }}
              >
                Reimposta
              </Button>
            </Box>

            <Divider />
            <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>
              Se ha perso il telefono e i codici di recupero, questo è l'unico modo per rimetterlo dentro: il codice non
              verrà più chiesto e potrà riconfigurarlo da capo. Accertati di sapere con chi stai parlando.
            </Typography>
            <Box>
              <Button
                variant="outlined"
                color="error"
                onClick={() => setConfermaReset2Fa(true)}
                disabled={reset2Fa.isPending}
                sx={{ whiteSpace: 'nowrap' }}
              >
                Azzera verifica in due passaggi
              </Button>
            </Box>
          </>
        ) : (
          <Typography sx={{ fontSize: 12, color: tokens.textTertiary }}>Nessun utente amministratore trovato per questo Cliente.</Typography>
        )}
      </DialogContent>
      {confermaReset2Fa && (
        <ConfirmDialog
          titolo="Azzerare la verifica in due passaggi?"
          messaggio={`${adminUtente?.email ?? 'Questo utente'} tornerà a entrare con la sola password e dovrà riconfigurare l'app da zero. I codici di recupero e i dispositivi ricordati vengono cancellati. Fallo solo se sei sicuro di chi te lo sta chiedendo.`}
          testoConferma="Azzera"
          inCorso={reset2Fa.isPending}
          onConferma={azzeraDueFattori}
          onAnnulla={() => setConfermaReset2Fa(false)}
        />
      )}

      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          Salva
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function NuovoClienteDialog({ onClose }: { onClose: () => void }) {
  const mobile = useMobile()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [nome, setNome] = useState('')
  const [cognome, setCognome] = useState('')
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
  const [ragioneSociale, setRagioneSociale] = useState('')
  const [partitaIva, setPartitaIva] = useState('')
  const [errore, setErrore] = useState<string | null>(null)
  const [salvataggioInCorso, setSalvataggioInCorso] = useState(false)

  const creaCliente = useCreaCliente()
  const creaUtente = useCreaUtente()
  const queryClient = useQueryClient()

  async function salva() {
    if (email.trim() === '' || password.trim().length < 8) {
      setErrore('Email obbligatoria, password di almeno 8 caratteri.')
      return
    }
    if (!isSuperAdmin && ragioneSociale.trim() === '') {
      setErrore('Indica la ragione sociale del Cliente.')
      return
    }
    setErrore(null)
    setSalvataggioInCorso(true)
    try {
      // Il Super Admin crea solo il Cliente e il suo primo utente, che è il titolare (accesso
      // libero a tutte le Strutture che verranno create per questo Cliente, non solo a quelle a cui
      // viene esplicitamente assegnato) — la gestione degli altri utenti/permessi per singola
      // struttura resta poi al Cliente stesso, non riguarda più il Super Admin.
      let clienteIdFinale: string | null = null
      if (!isSuperAdmin) {
        clienteIdFinale = (await creaCliente.mutateAsync({ ragioneSociale: ragioneSociale.trim(), partitaIva: partitaIva.trim() || null })).id
      }

      await creaUtente.mutateAsync({
        email: email.trim(),
        password: password.trim(),
        nome: nome.trim() || null,
        cognome: cognome.trim() || null,
        isSuperAdmin,
        clienteId: clienteIdFinale,
        isClienteAccount: !isSuperAdmin,
      })

      queryClient.invalidateQueries({ queryKey: ['super-admin', 'dashboard'] })
      onClose()
    } catch (err) {
      setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')
    } finally {
      setSalvataggioInCorso(false)
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth fullScreen={mobile}>
      <DialogTitle>Nuovo Cliente</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        {errore && <Alert severity="error">{errore}</Alert>}
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Crea il Cliente e le credenziali del suo primo utente, che sarà amministratore delle strutture che creerà.
        </Typography>
        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth disabled={salvataggioInCorso} autoFocus />
          <TextField label="Password" type="text" value={password} onChange={(e) => setPassword(e.target.value)} fullWidth disabled={salvataggioInCorso} />
        </Box>
        <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
          <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={salvataggioInCorso} />
          <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={salvataggioInCorso} />
        </Box>
        <FormControlLabel
          control={<Checkbox checked={isSuperAdmin} onChange={(e) => setIsSuperAdmin(e.target.checked)} disabled={salvataggioInCorso} />}
          label="Super Admin (staff GestiSoft, non appartiene a un Cliente)"
        />
        {!isSuperAdmin && (
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField label="Ragione sociale" value={ragioneSociale} onChange={(e) => setRagioneSociale(e.target.value)} fullWidth disabled={salvataggioInCorso} />
            <TextField label="P.IVA (opzionale)" value={partitaIva} onChange={(e) => setPartitaIva(e.target.value)} fullWidth disabled={salvataggioInCorso} />
          </Box>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={salvataggioInCorso}>
          Annulla
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={salvataggioInCorso}>
          Crea
        </Button>
      </DialogActions>
    </Dialog>
  )
}

function EliminaStrutturaDialog({
  struttura,
  clienteRagioneSociale,
  onClose,
  onEliminata,
}: {
  struttura: StrutturaAdminDto
  clienteRagioneSociale: string
  onClose: () => void
  /** Solo quando l'eliminazione va davvero a buon fine — distinto da onClose, chiamato anche su Annulla. */
  onEliminata?: () => void
}) {
  const [conferma, setConferma] = useState('')
  const toast = useToast()
  const elimina = useEliminaStrutturaDefinitivamente()

  const confermaValida = conferma.trim() === struttura.nome

  function procedi() {
    if (!confermaValida) return
    elimina.mutate(struttura.id, {
      onSuccess: () => (onEliminata ?? onClose)(),
      onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ color: tokens.error600 }}>Elimina definitivamente "{struttura.nome}"</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
        <Alert severity="error">
          Questa azione è <strong>irreversibile</strong>: cancella per sempre la struttura "{struttura.nome}" del Cliente "{clienteRagioneSociale}"
          e tutti i dati collegati (camere, prenotazioni, ospiti, fatture, integrazioni). Non è un semplice disattiva/riattiva.
        </Alert>
        <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary }}>
          Per confermare, scrivi il nome esatto della struttura: <strong>{struttura.nome}</strong>
        </Typography>
        <TextField
          value={conferma}
          onChange={(e) => setConferma(e.target.value)}
          fullWidth
          disabled={elimina.isPending}
          autoFocus
          placeholder={struttura.nome}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={elimina.isPending}>
          Annulla
        </Button>
        <Button variant="contained" color="error" onClick={procedi} disabled={!confermaValida || elimina.isPending}>
          Elimina definitivamente
        </Button>
      </DialogActions>
    </Dialog>
  )
}
