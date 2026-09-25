import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Skeleton from '@mui/material/Skeleton'
import Typography from '@mui/material/Typography'
import { ApiError } from '../../api/client'
import {
  StatoServizioSoggiorno,
  useSegnaServizioSoggiorno,
  useSoggiorniPulizie,
  type ServizioSoggiorno,
  type ServizioSoggiornoDto,
  type SoggiornoPulizieDto,
} from '../../api/pulizie'
import { fontDisplay, fontMono, stileImporto, tokens } from '../../theme'
import { useToast } from '../../toast/ToastContext'
import { MessaggioVuotoElenco } from '../CardElenco'

const formattatoreGiorno = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: '2-digit', month: '2-digit' })

// Le date arrivano come mezzanotte UTC di una data civile: si legge la data, non l'istante, per non
// farla scivolare al giorno prima.
function giorno(iso: string): string {
  const [a, m, g] = iso.slice(0, 10).split('-').map(Number)
  return formattatoreGiorno.format(new Date(a, m - 1, g))
}

function daFare(s: ServizioSoggiornoDto): boolean {
  return s.stato === StatoServizioSoggiorno.DaFareOggi || s.stato === StatoServizioSoggiorno.InRitardo
}

/** Prima le camere con qualcosa da fare oggi o in ritardo, poi le altre nell'ordine delle camere. */
function priorita(s: SoggiornoPulizieDto): number {
  const ritardo = s.pulizia.stato === StatoServizioSoggiorno.InRitardo || s.biancheria.stato === StatoServizioSoggiorno.InRitardo
  if (ritardo) return 0
  return daFare(s.pulizia) || daFare(s.biancheria) ? 1 : 2
}

/**
 * Camere occupate con pulizia e cambio biancheria durante il soggiorno, per chi le rifà. Frequenze
 * da Impostazioni e Tipologie, rinunce dalla prenotazione: qui si vede solo cosa tocca e si segna
 * come fatto.
 */
export function CamereOccupate({ strutturaId }: { strutturaId: string | null }) {
  const soggiorni = useSoggiorniPulizie(strutturaId)
  const segna = useSegnaServizioSoggiorno(strutturaId)
  const toast = useToast()

  function segnaFatto(s: SoggiornoPulizieDto, servizio: ServizioSoggiorno) {
    segna.mutate(
      { prenotazioneId: s.prenotazioneId, servizio },
      {
        onSuccess: () =>
          toast.successo(`${s.cameraNome ?? 'Camera'}: ${servizio === 'Pulizia' ? 'pulizia segnata come fatta' : 'cambio biancheria segnato'}.`),
        onError: (err) => toast.errore(err instanceof ApiError ? err.message : 'Operazione non riuscita, riprova.'),
      },
    )
  }

  if (soggiorni.isLoading) {
    return <Skeleton variant="rounded" height={160} />
  }

  const elenco = [...(soggiorni.data ?? [])].sort((a, b) => priorita(a) - priorita(b))
  if (elenco.length === 0) {
    return <MessaggioVuotoElenco messaggio="Nessuna camera occupata in questo momento." />
  }

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }, gap: 2 }}>
      {elenco.map((s) => {
        const urgente = priorita(s) < 2
        return (
          <Box
            key={s.prenotazioneId}
            sx={{
              border: `1.5px solid ${priorita(s) === 0 ? tokens.error600 : urgente ? tokens.orange600 : tokens.surfaceBorder}`,
              borderRadius: 2,
              bgcolor: tokens.surface,
              p: 2.5,
              display: 'flex',
              flexDirection: 'column',
              gap: 1.5,
            }}
          >
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 1 }}>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontFamily: fontDisplay, fontWeight: 700, fontSize: 17 }}>{s.cameraNome ?? '—'}</Typography>
                {s.tipologiaNome && <Typography sx={{ fontSize: 12.5, color: tokens.textSecondary, mt: 0.25 }}>{s.tipologiaNome}</Typography>}
              </Box>
              {s.notteCorrente != null && s.nottiTotali != null && (
                <Typography sx={{ ...stileImporto, fontSize: 12.5, color: tokens.textSecondary, whiteSpace: 'nowrap' }}>
                  notte {s.notteCorrente} di {s.nottiTotali}
                </Typography>
              )}
            </Box>

            <Box sx={{ display: 'flex', gap: 2.5, flexWrap: 'wrap' }}>
              <Meta etichetta="Ospiti" valore={s.numeroOspiti != null ? String(s.numeroOspiti) : '—'} />
              <Meta etichetta="Partenza" valore={s.partenza ? giorno(s.partenza) : '—'} />
            </Box>

            <RigaServizio titolo="Pulizia" servizio={s.pulizia} etichettaFatto="Pulizia fatta" inCorso={segna.isPending} onFatto={() => segnaFatto(s, 'Pulizia')} />
            <RigaServizio
              titolo="Biancheria"
              servizio={s.biancheria}
              etichettaFatto="Biancheria cambiata"
              inCorso={segna.isPending}
              onFatto={() => segnaFatto(s, 'Biancheria')}
            />
          </Box>
        )
      })}
    </Box>
  )
}

function Meta({ etichetta, valore }: { etichetta: string; valore: string }) {
  return (
    <Box>
      <Typography sx={{ fontSize: 11, color: tokens.textTertiary }}>{etichetta}</Typography>
      <Typography sx={{ fontSize: 13.5, fontWeight: 600 }}>{valore}</Typography>
    </Box>
  )
}

function ChipServizio({ servizio }: { servizio: ServizioSoggiornoDto }) {
  switch (servizio.stato) {
    case StatoServizioSoggiorno.DaFareOggi:
      return <Chip size="small" label="Oggi" sx={{ bgcolor: tokens.orange100, color: tokens.orange700, fontWeight: 700 }} />
    case StatoServizioSoggiorno.InRitardo:
      return (
        <Chip
          size="small"
          label={servizio.previsto ? `In ritardo, era ${giorno(servizio.previsto)}` : 'In ritardo'}
          sx={{ bgcolor: tokens.error100, color: tokens.error600, fontWeight: 700 }}
        />
      )
    case StatoServizioSoggiorno.Programmato:
      return <Chip size="small" label={servizio.previsto ? giorno(servizio.previsto) : 'Programmata'} sx={{ bgcolor: tokens.paper, color: tokens.textSecondary }} />
    case StatoServizioSoggiorno.Rinunciato:
      return <Chip size="small" label="L'ospite ha rinunciato" sx={{ bgcolor: tokens.paper, color: tokens.textTertiary }} />
    default:
      return <Chip size="small" label="Non prevista" sx={{ bgcolor: tokens.paper, color: tokens.textTertiary }} />
  }
}

function RigaServizio({
  titolo,
  servizio,
  etichettaFatto,
  inCorso,
  onFatto,
}: {
  titolo: string
  servizio: ServizioSoggiornoDto
  etichettaFatto: string
  inCorso: boolean
  onFatto: () => void
}) {
  // Anche in anticipo: capita di rifare una camera un giorno prima, e la successiva si conta da lì.
  const segnabile =
    servizio.stato === StatoServizioSoggiorno.DaFareOggi ||
    servizio.stato === StatoServizioSoggiorno.InRitardo ||
    servizio.stato === StatoServizioSoggiorno.Programmato

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', borderTop: `1px solid ${tokens.surfaceBorder}`, pt: 1.25 }}>
      <Typography sx={{ fontSize: 13, fontWeight: 700, minWidth: 76 }}>{titolo}</Typography>
      <ChipServizio servizio={servizio} />
      {servizio.ultimoFatto && (
        <Typography sx={{ fontSize: 11.5, color: tokens.textTertiary, fontFamily: fontMono }}>ultima {giorno(servizio.ultimoFatto)}</Typography>
      )}
      {segnabile && (
        <Button
          size="small"
          variant={servizio.stato === StatoServizioSoggiorno.Programmato ? 'outlined' : 'contained'}
          onClick={onFatto}
          disabled={inCorso}
          sx={{ ml: 'auto' }}
        >
          {etichettaFatto}
        </Button>
      )}
    </Box>
  )
}
