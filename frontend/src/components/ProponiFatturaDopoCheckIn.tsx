import { useState } from 'react'
import type { PrenotazioneDto } from '../api/prenotazioni'
import { ConfirmDialog } from './ConfirmDialog'
import { FatturaDialog } from './FatturaDialog'

/**
 * Subito dopo il check-in chiede se emettere il documento per il soggiorno: da quel momento c'è
 * qualcosa da fatturare, ed è quando l'ospite è davanti al banco. Rispondendo sì si apre il dialogo
 * della fattura con la prenotazione già scelta, che a sua volta apre il modulo del cliente se
 * mancano i suoi dati. Va mostrato solo a chi può scrivere in Finanze (usePuoScrivere('financeWrite')):
 * a un addetto al check-in senza quel permesso la domanda non va fatta.
 */
export function ProponiFatturaDopoCheckIn({
  strutturaId,
  prenotazione,
  onChiudi,
}: {
  strutturaId: string
  prenotazione: PrenotazioneDto
  onChiudi: () => void
}) {
  const [fase, setFase] = useState<'domanda' | 'fattura'>('domanda')

  if (fase === 'domanda') {
    const ospite = [prenotazione.ospiteNome, prenotazione.ospiteCognome].filter(Boolean).join(' ')
    return (
      <ConfirmDialog
        titolo="Vuoi generare la fattura?"
        messaggio={`Check-in registrato${ospite ? ` per ${ospite}` : ''}. Puoi emettere adesso la fattura o la ricevuta del soggiorno, oppure farlo più tardi dalla pagina Fatture o dalla scheda ospiti.`}
        testoConferma="Sì, genera"
        pericoloso={false}
        onConferma={() => setFase('fattura')}
        onAnnulla={onChiudi}
      />
    )
  }

  return (
    <FatturaDialog
      strutturaId={strutturaId}
      stato={{ modo: 'crea' }}
      prenotazioniDisponibili={[prenotazione]}
      clienti={[]}
      onClose={onChiudi}
    />
  )
}
