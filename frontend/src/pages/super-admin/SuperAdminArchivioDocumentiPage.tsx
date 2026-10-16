import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
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
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdfOutlined'
import CodeIcon from '@mui/icons-material/CodeOutlined'
import { useStruttura } from '../../struttura/StrutturaContext'
import { ApiError } from '../../api/client'
import {
  ETICHETTA_TIPO_DOCUMENTO_ARCHIVIATO,
  scaricaArchivioCliente,
  scaricaDocumentoArchiviato,
  useArchivioDocumenti,
  type DocumentoArchiviatoDto,
} from '../../api/superAdmin'
import { fontDisplay, stileImporto, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'

const formattatoreData = new Intl.DateTimeFormat('it-IT', { day: '2-digit', month: '2-digit', year: 'numeric' })
const formattatoreValuta = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' })

function data(giorno: string): string {
  return formattatoreData.format(new Date(`${giorno}T00:00:00`))
}

/**
 * I documenti fiscali delle strutture eliminate definitivamente: si ritrovano e si restituiscono al
 * Cliente. Ogni download resta nel Log. Si cancellano da soli dopo 10 anni.
 */
export function SuperAdminArchivioDocumentiPage() {
  const { isSuperAdmin } = useStruttura()
  const toast = useToast()
  const archivio = useArchivioDocumenti(isSuperAdmin)
  const [clienteId, setClienteId] = useState('')
  const [ricerca, setRicerca] = useState('')

  if (!isSuperAdmin) {
    return <Alert severity="error">Questa pagina è riservata al Super Admin.</Alert>
  }

  const documenti = archivio.data ?? []
  const clienti = [...new Map(documenti.map((d) => [d.clienteId, d.clienteRagioneSociale])).entries()].sort((a, b) => a[1].localeCompare(b[1]))
  const testo = ricerca.trim().toLowerCase()
  const filtrati = documenti.filter(
    (d) =>
      (!clienteId || d.clienteId === clienteId) &&
      // Anche data e importo, scritti come in tabella ("16/10/2026", "1.093,00 €").
      (!testo ||
        [d.strutturaNome, d.numero, d.controparte, d.strutturaPartitaIva, data(d.data), d.importo != null ? formattatoreValuta.format(d.importo) : null].some(
          (campo) => campo?.toLowerCase().includes(testo),
        )),
  )

  async function scarica(fn: () => Promise<void>) {
    try {
      await fn()
    } catch (err) {
      toast.errore(err instanceof ApiError ? err.message : 'Download non riuscito, riprova.')
    }
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
        <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 15 }}>Documenti delle strutture eliminate</Typography>
        <Typography sx={{ fontSize: 12.5, color: tokens.textTertiary }}>
          Fatture emesse e ricevute, note di credito e ricevute di locazione breve, copiati qui quando la struttura viene eliminata definitivamente. Servono a
          restituirli al Cliente se li chiede. Si cancellano da soli il 31 dicembre del decimo anno dalla data del documento. Ogni download resta nel Log.
        </Typography>
      </Box>

      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
        <TextField select size="small" label="Cliente" value={clienteId} onChange={(e) => setClienteId(e.target.value)} sx={{ minWidth: 260 }}>
          <MenuItem value="">Tutti</MenuItem>
          {clienti.map(([id, nome]) => (
            <MenuItem key={id} value={id}>
              {nome}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          size="small"
          label="Cerca"
          placeholder="Struttura, numero, controparte..."
          value={ricerca}
          onChange={(e) => setRicerca(e.target.value)}
          sx={{ flex: 1, minWidth: 220 }}
        />
        {clienteId && (
          <Button variant="contained" color="primary" onClick={() => void scarica(() => scaricaArchivioCliente(clienteId))}>
            Scarica tutto il Cliente (zip)
          </Button>
        )}
      </Box>

      {archivio.isLoading && <Skeleton variant="rounded" height={220} />}
      {archivio.isError && <Alert severity="error">Impossibile leggere l'archivio.</Alert>}

      {!archivio.isLoading && (
        <Box sx={{ border: `1px solid ${tokens.surfaceBorder}`, borderRadius: 2, bgcolor: tokens.surface, overflowX: 'auto' }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Data</TableCell>
                <TableCell>Documento</TableCell>
                <TableCell>Controparte</TableCell>
                <TableCell>Struttura</TableCell>
                <TableCell>Cliente</TableCell>
                <TableCell align="right">Importo</TableCell>
                <TableCell>Scade</TableCell>
                <TableCell align="right">File</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filtrati.length === 0 && (
                <TableRow>
                  <TableCell colSpan={8} sx={{ textAlign: 'center', color: tokens.textSecondary, py: 4 }}>
                    {documenti.length === 0 ? 'Nessun documento archiviato.' : 'Nessun documento corrisponde ai filtri.'}
                  </TableCell>
                </TableRow>
              )}
              {filtrati.map((d) => (
                <RigaDocumento key={d.id} documento={d} onScarica={(pdf) => void scarica(() => scaricaDocumentoArchiviato(d, pdf))} />
              ))}
            </TableBody>
          </Table>
        </Box>
      )}
    </Box>
  )
}

function RigaDocumento({ documento: d, onScarica }: { documento: DocumentoArchiviatoDto; onScarica: (pdf: boolean) => void }) {
  return (
    <TableRow hover>
      <TableCell sx={{ ...stileImporto }}>{data(d.data)}</TableCell>
      <TableCell>
        {ETICHETTA_TIPO_DOCUMENTO_ARCHIVIATO[d.tipo] ?? 'Documento'} n. {d.numero}
      </TableCell>
      <TableCell>{d.controparte ?? '—'}</TableCell>
      <TableCell>
        {d.strutturaNome}
        {d.strutturaPartitaIva && (
          <Box component="span" sx={{ display: 'block', fontSize: 11, color: tokens.textTertiary }}>
            {d.strutturaPartitaIva}
          </Box>
        )}
      </TableCell>
      <TableCell>{d.clienteRagioneSociale}</TableCell>
      <TableCell align="right" sx={{ ...stileImporto }}>
        {d.importo != null ? formattatoreValuta.format(d.importo) : '—'}
      </TableCell>
      <TableCell sx={{ ...stileImporto }}>{data(d.scadenza)}</TableCell>
      <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
        {d.haPdf && (
          <Tooltip title="Scarica PDF">
            <IconButton size="small" onClick={() => onScarica(true)}>
              <PictureAsPdfIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        {d.haFile && (
          <Tooltip title="Scarica il file elettronico">
            <IconButton size="small" onClick={() => onScarica(false)}>
              <CodeIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </TableCell>
    </TableRow>
  )
}
