import { useStruttura } from '../struttura/StrutturaContext'
import { useMioPermessoStruttura, type PermessiStruttura } from '../api/utenti'

/**
 * True se l'utente corrente può eseguire un'azione di scrittura (crea/modifica/elimina) che
 * richiede il permesso indicato sulla struttura selezionata — usato per nascondere pulsanti come
 * "+ Nuova camera"/Modifica/Elimina quando l'utente ha solo il permesso di consultazione, non di
 * scrittura (es. un Receptionist con "Camere: solo Consulta" non deve vedere pulsanti che
 * fallirebbero comunque lato server con 403).
 */
export function usePuoScrivere(chiave: keyof PermessiStruttura): boolean {
  const { isSuperAdmin, strutturaId } = useStruttura()
  const mioPermesso = useMioPermessoStruttura(!isSuperAdmin ? strutturaId : null)
  return isSuperAdmin || mioPermesso.data?.[chiave] === true
}
