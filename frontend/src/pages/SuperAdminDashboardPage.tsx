import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import MenuItem from '@mui/material/MenuItem'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { useStruttura } from '../struttura/StrutturaContext'
import {
  EsitoIntegrazione,
  useAnniDisponibiliStatisticheSuperAdmin,
  useStatisticheSuperAdmin,
  type EsitoIntegrazioneDto,
  type LicenzaInScadenzaDto,
  type LicenzaScadutaDto,
} from '../api/statisticheSuperAdmin'
import { KpiCard } from '../components/KpiCard'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../lib/anni'
import { fontDisplay, fontMono, tokens } from '../theme'
import { useMobile } from '../lib/useMobile'
import { CardElenco, RigaCardMeta, TestataCardElenco } from '../components/CardElenco'

const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })
const formattatoreDataOra = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

const TESTO_ESITO: Record<EsitoIntegrazione, string> = {
  [EsitoIntegrazione.NonConcesso]: 'non concesso',
  [EsitoIntegrazione.NonConfigurato]: 'non configurato',
  [EsitoIntegrazione.Attesa]: 'in attesa',
  [EsitoIntegrazione.Errore]: 'errore',
  [EsitoIntegrazione.Attivo]: 'attivo',
}

const COLORE_ESITO: Record<EsitoIntegrazione, string> = {
  [EsitoIntegrazione.NonConcesso]: tokens.textTertiary,
  [EsitoIntegrazione.NonConfigurato]: tokens.textTertiary,
  [EsitoIntegrazione.Attesa]: tokens.wait600,
  [EsitoIntegrazione.Errore]: tokens.error600,
  [EsitoIntegrazione.Attivo]: tokens.ok600,
}

export function SuperAdminDashboardPage() {
  const mobile = useMobile()
  const { isSuperAdmin } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const anniDisponibili = useAnniDisponibiliStatisticheSuperAdmin(isSuperAdmin)
  // Su richiesta esplicita, il selettore propone gli anni con dati reali più l'anno corrente, sempre.
  const anniSelezionabili = anniConAnnoCorrente(anniDisponibili.data)

  const statistiche = useStatisticheSuperAdmin(isSuperAdmin, anno)

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  if (statistiche.isLoading) {
    return <Skeleton variant="rounded" height={480} />
  }

  if (statistiche.isError || !statistiche.data) {
    return <Alert severity="error">Impossibile caricare le statistiche. Riprova.</Alert>
  }

  const dati = statistiche.data

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 18 }}>Dashboard</Typography>
        <TextField select size="small" label="Anno" value={anno} onChange={(e) => setAnno(Number(e.target.value))} sx={{ minWidth: 110 }}>
          {anniSelezionabili.map((a) => (
            <MenuItem key={a} value={a}>
              {a}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(5, 1fr)' }, gap: 2 }}>
        <KpiCard etichetta="Clienti" valore={String(dati.panoramica.clientiTotali)} dettaglio={`${dati.panoramica.clientiAttivi} attivi`} />
        <KpiCard etichetta="Strutture" valore={String(dati.panoramica.struttureTotali)} dettaglio={`${dati.panoramica.struttureAttive} attive`} />
        <KpiCard
          etichetta="Incassi rinnovi (anno)"
          valore={formattatoreValuta.format(dati.incassiRinnoviPerMese.reduce((tot, m) => tot + m.importo, 0))}
          dettaglio="rinnovi licenza pagati"
          accento
        />
        <KpiCard
          etichetta="Da sollecitare"
          valore={String(dati.licenzeScadute.length)}
          dettaglio="licenze scadute o mai rinnovate"
          accento={dati.licenzeScadute.length > 0}
          coloreAccento="error"
        />
        <KpiCard
          etichetta="Integrazioni in errore"
          valore={String(dati.saluteIntegrazioni.filter((s) => haErrore(s)).length)}
          dettaglio="strutture da controllare"
          accento={dati.saluteIntegrazioni.some((s) => haErrore(s))}
          coloreAccento="error"
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 2.5, alignItems: 'start' }}>
        <CardGrafico titolo="Licenze scadute (da sollecitare)">
          {dati.licenzeScadute.length === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna licenza scaduta.</Typography>
          ) : mobile ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {dati.licenzeScadute.map((l: LicenzaScadutaDto) => (
                <CardElenco key={l.strutturaId}>
                  <TestataCardElenco
                    titolo={l.nomeStruttura}
                    sottotitolo={l.ragioneSocialeCliente}
                    azioneDestra={
                      <Box component="span" sx={{ fontFamily: fontMono, color: tokens.error600, fontWeight: 700, fontSize: 13 }}>
                        {l.scadenza ? formattatoreData.format(new Date(l.scadenza)) : 'mai rinnovata'}
                      </Box>
                    }
                  />
                </CardElenco>
              ))}
            </Box>
          ) : (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Struttura</TableCell>
                    <TableCell>Cliente</TableCell>
                    <TableCell align="right">Scadenza</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {dati.licenzeScadute.map((l: LicenzaScadutaDto) => (
                    <TableRow key={l.strutturaId} hover>
                      <TableCell sx={{ fontWeight: 600 }}>{l.nomeStruttura}</TableCell>
                      <TableCell sx={{ color: tokens.textSecondary }}>{l.ragioneSocialeCliente}</TableCell>
                      <TableCell align="right" sx={{ fontFamily: fontMono, color: tokens.error600, fontWeight: 700 }}>
                        {l.scadenza ? formattatoreData.format(new Date(l.scadenza)) : 'mai rinnovata'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </CardGrafico>

        <CardGrafico titolo="Licenze in scadenza (prossimi 30 giorni)">
          {dati.licenzeInScadenza.length === 0 ? (
            <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna licenza in scadenza a breve.</Typography>
          ) : mobile ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {dati.licenzeInScadenza.map((l: LicenzaInScadenzaDto) => (
                <CardElenco key={l.strutturaId}>
                  <TestataCardElenco
                    titolo={l.nomeStruttura}
                    sottotitolo={l.ragioneSocialeCliente}
                    azioneDestra={
                      <Box component="span" sx={{ fontFamily: fontMono, color: tokens.wait600, fontWeight: 700, fontSize: 13 }}>
                        {l.giorniRimanenti} gg
                      </Box>
                    }
                  />
                  <RigaCardMeta voci={[{ etichetta: 'Scadenza', valore: formattatoreData.format(new Date(l.scadenza)) }]} />
                </CardElenco>
              ))}
            </Box>
          ) : (
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Struttura</TableCell>
                    <TableCell>Cliente</TableCell>
                    <TableCell align="right">Scadenza</TableCell>
                    <TableCell align="right">Giorni</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {dati.licenzeInScadenza.map((l: LicenzaInScadenzaDto) => (
                    <TableRow key={l.strutturaId} hover>
                      <TableCell sx={{ fontWeight: 600 }}>{l.nomeStruttura}</TableCell>
                      <TableCell sx={{ color: tokens.textSecondary }}>{l.ragioneSocialeCliente}</TableCell>
                      <TableCell align="right" sx={{ fontFamily: fontMono }}>
                        {formattatoreData.format(new Date(l.scadenza))}
                      </TableCell>
                      <TableCell align="right" sx={{ fontFamily: fontMono, color: tokens.wait600, fontWeight: 700 }}>
                        {l.giorniRimanenti}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </CardGrafico>
      </Box>

      <CardGrafico titolo="Salute integrazioni">
        {mobile ? (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            {dati.saluteIntegrazioni.length === 0 && (
              <Typography sx={{ fontSize: 13.5, color: tokens.textSecondary }}>Nessuna struttura.</Typography>
            )}
            {dati.saluteIntegrazioni.map((s) => (
              <CardElenco key={s.strutturaId}>
                <TestataCardElenco titolo={s.nomeStruttura} sottotitolo={s.ragioneSocialeCliente} />
                <RigaCardMeta
                  voci={[
                    { etichetta: 'Alloggiati Web', valore: <PallinoEsito esito={s.alloggiatiWeb} /> },
                    { etichetta: 'Osservatorio', valore: <PallinoEsito esito={s.osservatorio} /> },
                    { etichetta: 'PayTourist', valore: <PallinoEsito esito={s.payTourist} /> },
                    { etichetta: 'OTA', valore: <PallinoEsito esito={s.wubook} /> },
                  ]}
                />
              </CardElenco>
            ))}
          </Box>
        ) : (
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Struttura</TableCell>
                  <TableCell>Cliente</TableCell>
                  <TableCell>Alloggiati Web</TableCell>
                  <TableCell>Osservatorio</TableCell>
                  <TableCell>PayTourist</TableCell>
                  <TableCell>OTA</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {dati.saluteIntegrazioni.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                      Nessuna struttura.
                    </TableCell>
                  </TableRow>
                )}
                {dati.saluteIntegrazioni.map((s) => (
                  <TableRow key={s.strutturaId} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{s.nomeStruttura}</TableCell>
                    <TableCell sx={{ color: tokens.textSecondary }}>{s.ragioneSocialeCliente}</TableCell>
                    <TableCell>
                      <PallinoEsito esito={s.alloggiatiWeb} />
                    </TableCell>
                    <TableCell>
                      <PallinoEsito esito={s.osservatorio} />
                    </TableCell>
                    <TableCell>
                      <PallinoEsito esito={s.payTourist} />
                    </TableCell>
                    <TableCell>
                      <PallinoEsito esito={s.wubook} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </CardGrafico>
    </Box>
  )
}

function haErrore(s: { alloggiatiWeb: EsitoIntegrazioneDto; osservatorio: EsitoIntegrazioneDto; payTourist: EsitoIntegrazioneDto; wubook: EsitoIntegrazioneDto }) {
  return [s.alloggiatiWeb, s.osservatorio, s.payTourist, s.wubook].some((e) => e.stato === EsitoIntegrazione.Errore)
}

function PallinoEsito({ esito }: { esito: EsitoIntegrazioneDto }) {
  const contenuto = (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
      <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: COLORE_ESITO[esito.stato] }} />
      <Typography sx={{ fontSize: 12, color: tokens.textSecondary }}>{TESTO_ESITO[esito.stato]}</Typography>
    </Box>
  )

  if (!esito.ultimoErrore && !esito.ultimoInvioAtUtc) {
    return contenuto
  }

  const titolo = esito.ultimoErrore ?? `Ultimo invio: ${formattatoreDataOra.format(new Date(esito.ultimoInvioAtUtc!))}`
  return <Tooltip title={titolo}>{contenuto}</Tooltip>
}

function CardGrafico({ titolo, children }: { titolo: string; children: React.ReactNode }) {
  return (
    <Box sx={{ bgcolor: tokens.surface, border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, p: 3 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15.5, mb: 1.75 }}>{titolo}</Typography>
      {children}
    </Box>
  )
}
