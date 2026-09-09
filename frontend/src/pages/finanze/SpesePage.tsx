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
import { useSpese, useEliminaSpesa, type SpesaDto } from '../../api/spese'
import { ApiError } from '../../api/client'
import { fontMono, tokens } from '../../theme'
import { anniConAnnoCorrente, ANNO_CORRENTE } from '../../lib/anni'
import { useMobile } from '../../lib/useMobile'
import { SpesaDialog } from '../../components/SpesaDialog'
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

export function SpesePage() {
  const mobile = useMobile()
  const { strutturaId } = useStruttura()
  const puoScrivere = usePuoScrivere('financeWrite')
  const [anno, setAnno] = useState(ANNO_CORRENTE)
  const [errore, setErrore] = useState<string | null>(null)
  const [dialogo, setDialogo] = useState<'chiuso' | 'nuova' | SpesaDto>('chiuso')
  const [daEliminare, setDaEliminare] = useState<SpesaDto | null>(null)
  const [ricerca, setRicerca] = useState('')
  const [dataDa, setDataDa] = useState('')
  const [dataA, setDataA] = useState('')

  const anniDisponibili = useAnniDisponibiliFinanze(strutturaId)
  const anni = anniConAnnoCorrente(anniDisponibili.data)
  const spese = useSpese(strutturaId, anno)
  const elimina = useEliminaSpesa(strutturaId)

  function confermaEliminaSpesa() {
    if (!daEliminare) return
    elimina.mutate(daEliminare.id, {
      onSuccess: () => setDaEliminare(null),
      onError: (err) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
    })
  }

  const testoRicerca = ricerca.trim().toLowerCase()
  const dati = (spese.data ?? []).filter(
    (s) =>
      (!testoRicerca || [s.nome, s.tipoSpesa, s.metodoPagamento, s.descrizione].some((campo) => campo?.toLowerCase().includes(testoRicerca))) &&
      nelRangeData(s.dataSpesa, dataDa, dataA),
  )
  const totale = dati.reduce((acc, s) => acc + s.importoSpesa, 0)
  const { righeVisibili, altreDaCaricare, sentinellaRef } = usePaginazioneScroll(dati.length, [anno, testoRicerca, dataDa, dataA])
  const datiVisibili = dati.slice(0, righeVisibili)

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <IntestazioneFinanze
        anno={anno}
        anni={anni}
        onAnnoChange={setAnno}
        azioni={puoScrivere ? <AzioneNuovo etichetta="+ Nuova spesa" onClick={() => setDialogo('nuova')} disabilitato={!strutturaId} /> : undefined}
      />

      <FiltriRicercaData
        ricerca={ricerca}
        onRicercaChange={setRicerca}
        placeholderRicerca="Cerca per nome, tipo o metodo di pagamento..."
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

      {spese.isLoading && <Skeleton variant="rounded" height={220} />}

      {!spese.isLoading && mobile && (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          {dati.length === 0 && (
            <MessaggioVuotoElenco
              messaggio={testoRicerca || dataDa || dataA ? 'Nessuna spesa corrisponde ai filtri applicati.' : "Nessuna spesa registrata per l'anno selezionato."}
            />
          )}
          {datiVisibili.map((s) => (
            <CardElenco key={s.id}>
              <TestataCardElenco
                titolo={s.nome}
                sottotitolo={s.tipoSpesa ?? undefined}
                azioneDestra={
                  <Box component="span" sx={{ fontFamily: fontMono, fontWeight: 700, fontSize: 15, color: tokens.error600 }}>
                    {formattatoreValuta.format(s.importoSpesa)}
                  </Box>
                }
              />
              <RigaCardMeta
                voci={[
                  { etichetta: 'Data', valore: s.dataSpesa ? formattatoreData.format(new Date(s.dataSpesa)) : '—' },
                  { etichetta: 'Metodo', valore: s.metodoPagamento ?? '—' },
                ]}
              />
              {puoScrivere && (
                <AzioniCardElenco>
                  <IconButton size="small" onClick={() => setDialogo(s)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => setDaEliminare(s)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </AzioniCardElenco>
              )}
            </CardElenco>
          ))}
          {altreDaCaricare && <SentinellaCaricamentoElenco ref={sentinellaRef} />}
        </Box>
      )}

      {!spese.isLoading && !mobile && (
        <Cornice>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Nome</TableCell>
                <TableCell>Tipo</TableCell>
                <TableCell>Metodo</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell align="right">Azioni</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {dati.length === 0 && (
                <RigaVuota
                  colSpan={6}
                  messaggio={
                    testoRicerca || dataDa || dataA ? 'Nessuna spesa corrisponde ai filtri applicati.' : "Nessuna spesa registrata per l'anno selezionato."
                  }
                />
              )}
              {datiVisibili.map((s) => (
                <TableRow key={s.id} hover>
                  <TableCell sx={{ fontFamily: fontMono }}>{s.dataSpesa ? formattatoreData.format(new Date(s.dataSpesa)) : '—'}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{s.nome}</TableCell>
                  <TableCell>{s.tipoSpesa ?? '—'}</TableCell>
                  <TableCell>{s.metodoPagamento ?? '—'}</TableCell>
                  <TableCell align="right" sx={{ fontFamily: fontMono, fontWeight: 700, color: tokens.error600 }}>
                    {formattatoreValuta.format(s.importoSpesa)}
                  </TableCell>
                  <TableCell align="right">
                    {puoScrivere && (
                      <>
                        <IconButton size="small" onClick={() => setDialogo(s)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                        <IconButton size="small" onClick={() => setDaEliminare(s)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {altreDaCaricare && <RigaCaricamentoAltri colSpan={6} ref={sentinellaRef} />}
            </TableBody>
          </Table>
        </Cornice>
      )}

      {!spese.isLoading && dati.length > 0 && <BarraTotale etichetta="Totale spese" valore={totale} colore={tokens.error600} />}

      {dialogo !== 'chiuso' && strutturaId && <SpesaDialog strutturaId={strutturaId} spesa={dialogo === 'nuova' ? null : dialogo} onClose={() => setDialogo('chiuso')} />}

      {daEliminare && (
        <ConfirmDialog
          titolo="Eliminare spesa"
          messaggio={`Eliminare la spesa "${daEliminare.nome}"?`}
          inCorso={elimina.isPending}
          onConferma={confermaEliminaSpesa}
          onAnnulla={() => setDaEliminare(null)}
        />
      )}
    </Box>
  )
}
