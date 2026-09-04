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
import { useStruttura } from '../struttura/StrutturaContext'
import { useEntrate, useEliminaEntrata, type EntrataDto } from '../api/entrate'
import { ApiError } from '../api/client'
import { fontMono, tokens } from '../theme'
import { EntrataDialog } from '../components/EntrataDialog'
import { ConfirmDialog } from '../components/ConfirmDialog'
import {
  ANNO_CORRENTE,
  AzioneNuovo,
  Cornice,
  FiltriRicercaData,
  formattatoreData,
  formattatoreValuta,
  IntestazioneFinanze,
  nelRangeData,
  RigaCaricamentoAltri,
  RigaVuota,
} from '../components/finanze/FinanzeComuni'
import { usePaginazioneScroll } from '../lib/usePaginazioneScroll'

export function EntratePage() {
  const { strutturaId } = useStruttura()
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const [errore, setErrore] = useState<string | null>(null)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | EntrataDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<EntrataDto | null>(null)
  const [ricerca, setRicerca] = useState('')
  const [dataDa, setDataDa] = useState('')
  const [dataA, setDataA] = useState('')

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
    (e) => (!testoRicerca || [e.nome, e.tipoEntrata, e.descrizione].some((campo) => campo?.toLowerCase().includes(testoRicerca))) && nelRangeData(e.data, dataDa, dataA),
  )
  const totale = dati.reduce((acc, e) => acc + e.importoEntrata, 0)
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(dati.length, [anno, testoRicerca, dataDa, dataA])
  const datiVisibili = dati.slice(0, righeVisibili)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze
        titolo="Entrate"
        anno={anno}
        onAnnoChange={setAnno}
        azioni={<AzioneNuovo etichetta="+ Nuova entrata" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} />}
      />

      <FiltriRicercaData
        ricerca={ricerca}
        onRicercaChange={setRicerca}
        placeholderRicerca="Cerca per nome o tipo..."
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

      {!entrate.isLoading && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Nome</TableCell>
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
                  <TableCell sx={{ fontFamily: fontMono }}>{e.data ? formattatoreData.format(new Date(e.data)) : '—'}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{e.nome}</TableCell>
                  <TableCell>{e.tipoEntrata ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700, color: tokens.ok600 }}>
                    {formattatoreValuta.format(e.importoEntrata)}
                  </TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => setDialogo(e)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => setDaEliminare(e)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={5} ref={sentinellaRef} />}
              {dati.length > 0 && (
                <TableRow>
                  <TableCell colSpan={3} />
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700 }}>
                    {formattatoreValuta.format(totale)}
                  </TableCell>
                  <TableCell />
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {dialogo !== 'chiuso' && strutturaId && <EntrataDialog strutturaId={strutturaId} entrata={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare entrata"
          messaggio={`Eliminare l'entrata "${daEliminare.nome}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaEntrata}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
