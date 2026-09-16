import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Skeleton from '@mui/material/Skeleton'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import EditIcon from '@mui/icons-material/EditOutlined'
import DeleteIcon from '@mui/icons-material/DeleteOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { useAnniDisponibiliFinanze } from '../../api/finanze'
import { useEntrate, useEliminaEntrata, type EntrataDto } from '../../api/entrate'
import { ApiError } from '../../api/client'
import { stileImporto, tokens } from '../../theme'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { EntrataDialog } from '../../components/EntrataDialog'
import { AzioniCardElenco, CardElenco, MessaggioVuotoElenco, RigaCardMeta, SentinellaCaricamentoElenco, TestataCardElenco } from '../../components/CardElenco'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import {
  AzioneNuovo,
  BarraTotale,
  Cornice,
  FiltriRicercaData,
  formattatoreData,
  formattatoreValuta,
  IntestazioneFinanze,
  nelRangeData,
  RigaCaricamentoAltri,
  RigaVuota,
} from '../../components/finanze/FinanzeComuni'
import { usePaginazioneScroll } from '../../lib/usePaginazioneScroll'
import { usePuoScrivere } from '../../permessi/usePuoScrivere'

export function EntratePage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoScrivere = usePuoScrivere('financeWrite')
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const [errore, setErrore] = useState<string | null>(null)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | EntrataDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<EntrataDto | null>(null)
  const [ricerca, setRicerca] = useState('')
  const [dataDa, setDataDa] = useState('')
  const [dataA, setDataA] = useState('')

  const anniDisponibili = useAnniDisponibiliFinanze(strutturaId)
  const anni = anniConAnnoCorrente(anniDisponibili.data)
  const entrate = useEntrate(strutturaId, anno)
  const elimina = useEliminaEntrata(strutturaId)

  function confermaEliminaEntrata() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, {
      onSuccess: () => setDaEliminare(null),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const testoRicerca = ricerca.trim().toLowerCase()
  const dati = (entrate.data ?? []).filter(
    (e) => (!testoRicerca || [e.tipoEntrata, e.descrizione].some((campo) => campo?.toLowerCase().includes(testoRicerca))) && nelRangeData(e.data, dataDa, dataA),
  )
  const totale = dati.reduce((acc, e) => acc + e.importoEntrata, 0)
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(dati.length, [anno, testoRicerca, dataDa, dataA])
  const datiVisibili = dati.slice(0, righeVisibili)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze
        anno={anno}
        anni={anni}
        onAnnoChange={setAnno}
        azioni={puoScrivere ? <AzioneNuovo etichetta="+ Nuova entrata" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} /> : undefined}
      />

      <FiltriRicercaData
        ricerca={ricerca}
        onRicercaChange={setRicerca}
        placeholderRicerca="Cerca per descrizione o tipo..."
        dataDa={dataDa}
        onDataDaChange={setDataDa}
        dataA={dataA}
        onDataAChange={setDataA}
      />

      {errore && (
        <Alert severity="error" onClose={() => setErrore(null)}>
          {errore}
        </Alert>
      )}

      {entrate.isLoading && <Skeleton variant="rounded" height={220} />}

      {!entrate.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {dati.length === 0 && (
            <MessaggioVuotoElenco
              messaggio={testoRicerca || dataDa || dataA ? 'Nessuna entrata corrisponde ai filtri applicati.' : "Nessuna entrata registrata per l'anno selezionato."}
            />
          )}
          {datiVisibili.map((e) => (
            <CardElenco key={e.id}>
              <TestataCardElenco
                titolo={e.descrizione ?? '—'}
                sottotitolo={e.tipoEntrata ?? undefined}
                azioneDestra={
                  <Box component="span" sx={{ ...stileImporto, fontWeight: 700, fontSize: 15, color: tokens.ok600 }}>
                    {formattatoreValuta.format(e.importoEntrata)}
                  </Box>
                }
              />
              <RigaCardMeta voci={[{ etichetta: 'Data', valore: e.data ? formattatoreData.format(new Date(e.data)) : '—' }]} />
              {puoScrivere && (
                <AzioniCardElenco>
                  <IconButton size="small" onClick={() => setDialogo(e)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => setDaEliminare(e)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
          {altreDaCaricare && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!entrate.isLoading && !mobile && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Descrizione</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dati.length === 0 && (
                <RigaVuota
                  colSpan={5}
                  messaggio={
                    testoRicerca || dataDa || dataA ? 'Nessuna entrata corrisponde ai filtri applicati.' : "Nessuna entrata registrata per l'anno selezionato."
                  }
                />
              )}
              {datiVisibili.map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell sx={{ ...stileImporto }}>{e.data ? formattatoreData.format(new Date(e.data)) : '—'}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{e.descrizione ?? '—'}</TableCell>
                  <TableCell>{e.tipoEntrata ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ ...stileImporto, fontWeight: 700, color: tokens.ok600 }}>
                    {formattatoreValuta.format(e.importoEntrata)}
                  </TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <>
                        <IconButton size="small" onClick={() => setDialogo(e)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton size="small" onClick={() => setDaEliminare(e)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={5} ref={sentinellaRef} />}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {!entrate.isLoading && dati.length > 0 && <BarraTotale etichetta="Totale entrate" valore={totale} colore={tokens.ok600} />}

      {dialogo !== 'chiuso' && strutturaId && <EntrataDialog strutturaId={strutturaId} entrata={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare entrata"
          messaggio={`Eliminare l'entrata "${daEliminare.descrizione ?? ''}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaEntrata}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
