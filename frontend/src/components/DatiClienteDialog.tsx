import { useEffect, useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ApiError } from '../api/client'
import { useCreaDatiCliente, useAggiornaDatiCliente, type DatiClienteDto, type DatiClienteRequest } from '../api/fatturazione'
import { Sesso } from '../api/ospiti'
import type { ComuneDto } from '../api/riferimenti'
import { calcolaCodiceFiscale } from '../lib/codiceFiscale'
import { formatoInputData, isoLocale, parsaInputData } from '../lib/date'
import { useMobile } from '../lib/useMobile'
import { CampoData } from './CampoData'
import { SelectComune } from './SelectComune'
import { fontDisplay, tokens } from '../theme'

interface Props {
  strutturaId: string
  cliente: DatiClienteDto | null
  /**
   * true quando `cliente` è solo l'anteprima proposta da "Genera fattura" (mai ancora scritta sul
   * database, vedi FatturazioneService.RisolviClientePerPrenotazioneAsync) — forza una vera creazione
   * (POST) anche se `cliente` non è null, invece di tentare un aggiornamento (PUT) su un Id che non
   * esiste ancora. La creazione reale avviene solo qui, quando l'operatore preme "Crea cliente".
   */
  clienteNonPersistito?: boolean
  onClose: () => void
}

function TitoloSezione({ children, nota }: { children: string; nota?: string }) {
  return (
    <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 13.5 }}>
      {children}
      {nota && (
        <Typography component="span" sx={{ fontFamily: 'inherit', fontWeight: 400, fontSize: 12, color: tokens.textTertiary, ml: 1 }}>
          — {nota}
        </Typography>
      )}
    </Typography>
  )
}

export function DatiClienteDialog({ strutturaId, cliente, clienteNonPersistito, onClose }: Props) {
  const mobile = useMobile()
  // Solo un cliente realmente salvato può essere aggiornato (PUT): una proposta non ancora
  // persistita va sempre creata da zero (POST), qui, quando l'operatore preme "Crea cliente" — mai
  // prima, altrimenti resterebbe un Cliente orfano ogni volta che si apre "Genera fattura" e si
  // annulla senza salvare.
  const clienteEsistente = cliente && !clienteNonPersistito ? cliente : null
  const [denominazione, setDenominazione] = useState(cliente?.denominazione ?? '')
  const [nome, setNome] = useState(cliente?.nome ?? '')
  const [cognome, setCognome] = useState(cliente?.cognome ?? '')
  const [pIva, setPIva] = useState(cliente?.pIva ?? '')
  const [codiceFiscale, setCodiceFiscale] = useState(cliente?.codiceFiscale ?? '')
  const [dataNascita, setDataNascita] = useState(cliente?.dataNascita ? formatoInputData(new Date(cliente.dataNascita)) : '')
  const [sesso, setSesso] = useState<string>(cliente?.sesso != null ? String(cliente.sesso) : '')
  const [luogoNascita, setLuogoNascita] = useState(cliente?.luogoNascita ?? '')
  const [belfioreNascita, setBelfioreNascita] = useState<string | null>(null)
  const [indirizzo, setIndirizzo] = useState(cliente?.indirizzo ?? '')
  const [nCivico, setNCivico] = useState(cliente?.nCivico ?? '')
  const [cap, setCap] = useState(cliente?.cap ?? '')
  const [luogoResidenza, setLuogoResidenza] = useState(cliente?.luogoResidenza ?? '')
  const [provincia, setProvincia] = useState(cliente?.provincia ?? '')
  const [cittadinanza, setCittadinanza] = useState(cliente?.cittadinanza ?? '')
  const [iso2, setIso2] = useState(cliente?.iso2 ?? '')
  const [codiceDestinatario, setCodiceDestinatario] = useState(cliente?.codiceDestinatario ?? '')
  const [pec, setPec] = useState(cliente?.pec ?? '')
  const [errore, setErrore] = useState<string | null>(null)

  const crea = useCreaDatiCliente(strutturaId)
  const aggiorna = useAggiornaDatiCliente(strutturaId)
  const inCorso = crea.isPending || aggiorna.isPending

  // Il Codice Fiscale suggerito si ricalcola in automatico finché l'operatore non lo modifica a
  // mano: confrontando il campo con l'ultimo suggerimento generato (invece di un flag booleano
  // separato) si distingue "non ancora toccato" da "corretto manualmente" senza doverlo resettare
  // esplicitamente quando l'operatore svuota di nuovo il campo.
  const ultimoCfAutomatico = useRef<string | null>(null)

  useEffect(() => {
    if (denominazione.trim() !== '') return // il calcolo vale solo per persone fisiche, non aziende
    if (iso2.trim() !== '' && iso2.trim().toUpperCase() !== 'IT') return // solo cittadini italiani
    if (!belfioreNascita || dataNascita === '' || sesso === '') return

    const suggerito = calcolaCodiceFiscale({
      cognome,
      nome,
      dataNascita: parsaInputData(dataNascita),
      sesso: Number(sesso) as 1 | 2,
      codiceBelfiore: belfioreNascita,
    })
    if (!suggerito) return

    if (codiceFiscale.trim() === '' || codiceFiscale === ultimoCfAutomatico.current) {
      ultimoCfAutomatico.current = suggerito
      setCodiceFiscale(suggerito)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cognome, nome, dataNascita, sesso, belfioreNascita, denominazione, iso2])

  function salva() {
    if (denominazione.trim() === '' && (nome.trim() === '' || cognome.trim() === '')) {
      setErrore('Indica una denominazione (azienda) oppure nome e cognome (privato).')
      return
    }
    setErrore(null)

    const vuoto = (v: string) => (v.trim() === '' ? null : v.trim())
    const request: DatiClienteRequest = {
      denominazione: vuoto(denominazione),
      nome: vuoto(nome),
      cognome: vuoto(cognome),
      pIva: vuoto(pIva),
      codiceFiscale: vuoto(codiceFiscale),
      dataNascita: dataNascita === '' ? null : isoLocale(parsaInputData(dataNascita)),
      sesso: sesso === '' ? null : Number(sesso),
      luogoNascita: vuoto(luogoNascita),
      indirizzo: vuoto(indirizzo),
      nCivico: vuoto(nCivico),
      cap: vuoto(cap),
      luogoResidenza: vuoto(luogoResidenza),
      provincia: vuoto(provincia),
      cittadinanza: vuoto(cittadinanza),
      iso2: vuoto(iso2),
      codiceDestinatario: vuoto(codiceDestinatario),
      pec: vuoto(pec),
      // Mai un campo modificabile in UI: se questa è la prima persistenza di una proposta da "Genera
      // fattura", porta con sé la stessa CustomerKey già calcolata lì, per continuare a deduplicare
      // per ospite; per un cliente creato manualmente da questa pagina resta null come sempre.
      customerKey: cliente?.customerKey ?? null,
    }

    const onError = (err: unknown) => setErrore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.')

    if (clienteEsistente) {
      aggiorna.mutate({ clienteId: clienteEsistente.id, request }, { onSuccess: onClose, onError })
    } else {
      crea.mutate(request, { onSuccess: onClose, onError })
    }
  }

  function onComuneResidenzaSelezionato(comune: ComuneDto | null) {
    if (!comune) return
    if (comune.provincia) setProvincia(comune.provincia)
    if (comune.cap) setCap(comune.cap)
  }

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth fullScreen={mobile}>
      <DialogTitle>{clienteEsistente ? 'Modifica cliente' : 'Nuovo cliente'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2.5, pt: 1 }}>
        <Box>{errore && <Alert severity="error">{errore}</Alert>}</Box>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TitoloSezione>Anagrafica</TitoloSezione>
          <TextField label="Denominazione (se azienda)" value={denominazione} onChange={(e) => setDenominazione(e.target.value)} disabled={inCorso} autoFocus />
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} fullWidth disabled={inCorso} />
            <TextField label="Cognome" value={cognome} onChange={(e) => setCognome(e.target.value)} fullWidth disabled={inCorso} />
          </Box>
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField label="Partita IVA" value={pIva} onChange={(e) => setPIva(e.target.value)} fullWidth disabled={inCorso} />
            <TextField label="Codice fiscale" value={codiceFiscale} onChange={(e) => setCodiceFiscale(e.target.value)} fullWidth disabled={inCorso} />
          </Box>
        </Box>

        <Divider />

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TitoloSezione nota="per il calcolo automatico del Codice Fiscale, se il cliente è italiano">Dati di nascita</TitoloSezione>
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <CampoData label="Data di nascita" value={dataNascita} onChange={setDataNascita} fullWidth disabled={inCorso} />
            <TextField select label="Sesso" value={sesso} onChange={(e) => setSesso(e.target.value)} sx={{ minWidth: 150 }} disabled={inCorso}>
              <MenuItem value="">—</MenuItem>
              <MenuItem value={String(Sesso.Maschio)}>Maschio</MenuItem>
              <MenuItem value={String(Sesso.Femmina)}>Femmina</MenuItem>
            </TextField>
            <SelectComune
              label="Comune di nascita"
              value={luogoNascita}
              onChange={setLuogoNascita}
              onComuneSelezionato={(c) => setBelfioreNascita(c?.codiceBelfiore ?? null)}
              disabled={inCorso}
            />
          </Box>
        </Box>

        <Divider />

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TitoloSezione>Residenza</TitoloSezione>
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField label="Indirizzo" value={indirizzo} onChange={(e) => setIndirizzo(e.target.value)} fullWidth disabled={inCorso} />
            <TextField label="N. civico" value={nCivico} onChange={(e) => setNCivico(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
            <TextField label="CAP" value={cap} onChange={(e) => setCap(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
          </Box>
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <SelectComune label="Comune di residenza" value={luogoResidenza} onChange={setLuogoResidenza} onComuneSelezionato={onComuneResidenzaSelezionato} disabled={inCorso} />
            <TextField label="Provincia" value={provincia} onChange={(e) => setProvincia(e.target.value)} sx={{ width: 110 }} disabled={inCorso} />
            <TextField label="Cittadinanza" value={cittadinanza} onChange={(e) => setCittadinanza(e.target.value)} fullWidth disabled={inCorso} />
          </Box>
        </Box>

        <Divider />

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <TitoloSezione>Fatturazione elettronica</TitoloSezione>
          <Box sx={{ display: 'flex', flexDirection: mobile ? 'column' : 'row', gap: 2 }}>
            <TextField label="Nazione (ISO2)" value={iso2} onChange={(e) => setIso2(e.target.value)} sx={{ width: 170 }} disabled={inCorso} />
            <TextField label="Codice destinatario SDI" value={codiceDestinatario} onChange={(e) => setCodiceDestinatario(e.target.value)} fullWidth disabled={inCorso} />
            <TextField label="PEC" value={pec} onChange={(e) => setPec(e.target.value)} fullWidth disabled={inCorso} />
          </Box>
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={inCorso}>
          Chiudi
        </Button>
        <Button variant="contained" color="primary" onClick={salva} disabled={inCorso}>
          {clienteEsistente ? 'Salva modifiche' : 'Crea cliente'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
