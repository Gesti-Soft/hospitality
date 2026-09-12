import { apiGet, ApiError } from '../api/client'
import type { OspiteDto } from '../api/ospiti'
import { useInviaSchedinaAlloggiatiWebSingola } from '../api/integrazioni'
import { useToast } from '../toast/ToastContext'
import { ConfirmDialog } from './ConfirmDialog'

/**
 * Un soggiorno che inizia e finisce nello stesso giorno dura meno di 24 ore: per legge la schedina
 * va trasmessa alla Polizia di Stato entro 6 ore dall'arrivo, non entro 24. L'invio automatico gira
 * una volta al giorno all'orario configurato, quindi in questi casi arriverebbe quasi sempre tardi —
 * per questo, appena registrato il check-in, si chiede all'operatore se trasmetterla subito.
 */
export function isSoggiornoBreve(checkIn: string | null, checkOut: string | null): boolean {
  if (!checkIn || !checkOut) return false
  return new Date(checkIn).toDateString() === new Date(checkOut).toDateString()
}

interface Props {
  strutturaId: string | null
  prenotazioneId: string
  /** Di chi è il soggiorno: nome dell'ospite se la scheda è già compilata, altrimenti il numero della prenotazione. Null se non si sa ancora nulla dei due. */
  riferimento?: string | null
  onChiudi: () => void
}

export function ConfermaSchedinaSoggiornoBreve({ strutturaId, prenotazioneId, riferimento, onChiudi }: Props) {
  const invia = useInviaSchedinaAlloggiatiWebSingola(strutturaId)
  const toast = useToast()

  async function inviaAdesso() {
    try {
      // La schedina si invia per Ospite (come l'elenco della pagina Polizia di Stato), mentre qui si
      // parte dalla prenotazione appena messa in corso: si risolve il capofamiglia al volo, senza
      // tenere una query aperta per ogni riga dell'elenco arrivi.
      const ospite = await apiGet<OspiteDto>(`/strutture/${strutturaId}/prenotazioni/${prenotazioneId}/ospiti`)
      invia.mutate(ospite.id, {
        onSuccess: (r) => {
          if (r.inviate > 0) {
            toast.successo('Schedina inviata alla Polizia di Stato.')
          } else {
            toast.errore(r.messaggio ?? 'Invio non riuscito: la schedina resta da trasmettere.')
          }
          onChiudi()
        },
        onError: (err) => {
          toast.errore(err instanceof ApiError ? err.message : 'Invio non riuscito.')
          onChiudi()
        },
      })
    } catch (err) {
      // Scheda ospiti non ancora compilata: non è un errore del check-in, va solo completata prima.
      toast.errore(
        err instanceof ApiError && err.status === 404
          ? 'Scheda ospiti non ancora compilata: completala per poter trasmettere la schedina.'
          : 'Invio non riuscito.',
      )
      onChiudi()
    }
  }

  return (
    <ConfirmDialog
      titolo="Soggiorno breve"
      messaggio={`${riferimento ? `Il soggiorno di ${riferimento}` : 'Questo soggiorno'} dura meno di 24 ore: la schedina va trasmessa alla Polizia di Stato entro 6 ore dall'arrivo, mentre l'invio automatico parte una volta al giorno. Vuoi inviarla adesso?`}
      testoConferma="Invia adesso"
      pericoloso={false}
      inCorso={invia.isPending}
      onConferma={inviaAdesso}
      onAnnulla={onChiudi}
    />
  )
}
