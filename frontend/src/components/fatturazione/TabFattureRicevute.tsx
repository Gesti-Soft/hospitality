import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import DownloadIcon from '@mui/icons-material/FileDownloadOutlined'
import { ApiError } from '../../api/client'
import { nomeTipoDocumentoRicevuto, scaricaFatturaRicevuta, useFattureRicevute, type FatturaRicevutaDto } from '../../api/fatturazione'
import { fontDisplay, fontMono, stileImporto, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { useMobile } from '../../lib/useMobile'
import { usePaginazioneScroll } from '../../lib/usePaginazioneScroll'
import { CardElenco, MessaggioVuotoElenco, RigaCardMeta, SentinellaCaricamentoElenco, TestataCardElenco } from '../CardElenco'
import { FiltriRicercaData, nelRangeData, RigaCaricamentoAltri } from '../finanze/FinanzeComuni'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })

function importo(f: FatturaRicevutaDto): string {
  const totale = f.importoTotale ?? f.imponibile + f.imposta
  return new Intl.NumberFormat('it-IT', { style: 'currency', currency: f.divisa || 'EUR' }).format(totale)
}

function data(f: FatturaRicevutaDto): string {
  return formattatoreData.format(new Date(`${f.data}T00:00:00`))
}

/**
 * Le fatture dei fornitori arrivate dallo SdI, separate da quelle emesse. Sono del Cliente: se ha
 * più strutture si vede a quale si appoggia ognuna. "Nuova" finché nessuno ha scaricato il file.
 */
export function TabFattureRicevute({ strutturaId, anno }: { strutturaId: string | null; anno: number }) {
  const mobile = useMobile()
  const toast = useToast()
  const queryClient = useQueryClient()
  const fatture = useFattureRicevute(strutturaId, anno)
  const [ricerca, setRicerca] = useState('')
  const [dataDa, setDataDa] = useState('')
  const [dataA, setDataA] = useState('')

  const piuStrutture = new Set((fatture.data ?? []).map((f) => f.strutturaId)).size > 1
  const testoRicerca = ricerca.trim().toLowerCase()
  const filtrate = (fatture.data ?? []).filter(
    (f) =>
      (!testoRicerca || [f.fornitore, f.numero, f.fornitorePartitaIva, f.fornitoreCodiceFiscale].some((campo) => campo?.toLowerCase().includes(testoRicerca))) &&
      nelRangeData(`${f.data}T00:00:00`, dataDa, dataA),
  )
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(filtrate.length, [testoRicerca, dataDa, dataA, fatture.data])
  const visibili = filtrate.slice(0, righeVisibili)
  const messaggioVuoto = testoRicerca || dataDa || dataA ? 'Nessuna fattura corrisponde ai filtri applicati.' : "Nessuna fattura ricevuta per l'anno selezionato."

  async function scarica(f: FatturaRicevutaDto) {
    if (!strutturaId) return
    try {
      await scaricaFatturaRicevuta(strutturaId, f)
      void queryClient.invalidateQueries({ queryKey: ['fatture-ricevute', strutturaId] })
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    }
  }

  const bottoneScarica = (f: FatturaRicevutaDto) => (
    <Tooltip title="Scarica il file ricevuto">
      <IconButton size="small" onClick={() => scarica(f)}>
        <DownloadIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  )
  const nuova = (f: FatturaRicevutaDto) => !f.lettaAtUtc && <Chip size="small" label="Nuova" color="info" sx={{ ml: 1 }} />

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Fatture ricevute dai fornitori</Typography>

      <FiltriRicercaData
        ricerca={ricerca}
        onRicercaChange={setRicerca}
        placeholderRicerca="Cerca per fornitore, numero o partita IVA..."
        dataDa={dataDa}
        onDataDaChange={setDataDa}
        dataA={dataA}
        onDataAChange={setDataA}
      />

      {fatture.isLoading && <Skeleton variant="rounded" height={220} />}

      {!fatture.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {filtrate.length === 0 && <MessaggioVuotoElenco messaggio={messaggioVuoto} />}
          {visibili.map((f) => (
            <CardElenco key={f.id}>
              <TestataCardElenco
                titolo={f.fornitore}
                sottotitolo={`${nomeTipoDocumentoRicevuto(f.tipoDocumento)} n. ${f.numero}`}
                azioneDestra={
                  <Box component="span" sx={{ ...stileImporto, fontWeight: 700, fontSize: 15 }}>
                    {importo(f)}
                  </Box>
                }
              />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Data', valore: data(f) },
                  ...(piuStrutture ? [{ etichetta: 'Struttura', valore: f.strutturaNome }] : []),
                ]}
              />
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <Box>{nuova(f)}</Box>
                {bottoneScarica(f)}
              </Box>
            </CardElenco>
          ))}
          {altreDaCaricare && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!fatture.isLoading && !mobile && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflow: 'hidden' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Fornitore</TableCell>
                <TableCell>Documento</TableCell>
                {piuStrutture && <TableCell>Struttura</TableCell>}
                <TableCell align="right">Imponibile</TableCell>
                <TableCell align="right">IVA</TableCell>
                <TableCell align="right">Totale</TableCell>
                <TableCell align="right">File</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filtrate.length === 0 && (
                <TableRow>
                  <TableCell colSpan={piuStrutture ? 8 : 7} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    {messaggioVuoto}
                  </TableCell>
                </TableRow>
              )}
              {visibili.map((f) => (
                <TableRow key={f.id} hover>
                  <TableCell sx={{ ...stileImporto }}>{data(f)}</TableCell>
                  <TableCell>
                    {f.fornitore}
                    {nuova(f)}
                    {(f.fornitorePartitaIva || f.fornitoreCodiceFiscale) && (
                      <Box component="span" sx={{ display: 'block', fontFamily: fontMono, fontSize: 11, color: tokens.textTertiary }}>
                        {f.fornitorePartitaIva ?? f.fornitoreCodiceFiscale}
                      </Box>
                    )}
                  </TableCell>
                  <TableCell>
                    {nomeTipoDocumentoRicevuto(f.tipoDocumento)} n. {f.numero}
                  </TableCell>
                  {piuStrutture && <TableCell>{f.strutturaNome}</TableCell>}
                  <TableCell align="right" sx={{ ...stileImporto }}>
                    {new Intl.NumberFormat('it-IT', { minimumFractionDigits: 2 }).format(f.imponibile)}
                  </TableCell>
                  <TableCell align="right" sx={{ ...stileImporto }}>
                    {new Intl.NumberFormat('it-IT', { minimumFractionDigits: 2 }).format(f.imposta)}
                  </TableCell>
                  <TableCell align="right" sx={{ ...stileImporto, fontWeight: 700 }}>
                    {importo(f)}
                  </TableCell>
                  <TableCell align="right">{bottoneScarica(f)}</TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={piuStrutture ? 8 : 7} ref={sentinellaRef} />}
            </TableBody>
          </Table>
        </Box>
      )}
    </Box>
  )
}
