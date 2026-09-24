import { useStruttura } from '../struttura/StrutturaContext'
import { useMioPermessoStruttura, type PermessiStruttura } from '../api/utenti'
import { navSections, type NavItem, type NavSection } from './navItems'

export interface SezioneVisibile {
  section: NavSection
  voci: NavItem[]
}

export interface SezioniVisibiliRisultato {
  sezioni: SezioneVisibile[]
  /** True finché i permessi dell'utente non sono ancora arrivati — evita di decidere un redirect (vedi RootRoute) su dati incompleti. */
  caricamento: boolean
}

/**
 * Sezioni/voci di menu visibili per l'utente corrente — stessa logica usata dalla barra di
 * navigazione (AppShell) e dal redirect della rotta "/" (AppRouter), per non duplicarla: un utente
 * senza alcun permesso per vedere il Cruscotto (es. addetto pulizie) non deve né trovarlo nel menu
 * né atterrarci dopo il login.
 */
export function useSezioniVisibili(): SezioniVisibiliRisultato {
  const { isSuperAdmin, strutturaId, strutturaCorrente } = useStruttura()
  // Il Super Admin non ha bisogno di questo dato, ha sempre tutti i permessi.
  const mioPermesso = useMioPermessoStruttura(!isSuperAdmin ? strutturaId : null)

  // Un array richiede TUTTI i permessi elencati (AND), non uno qualsiasi — vedi commento su `NavItem.richiedePermesso`.
  const haPermesso = (chiavi: keyof PermessiStruttura | (keyof PermessiStruttura)[] | undefined) => {
    if (!chiavi) return true
    if (isSuperAdmin) return true
    return (Array.isArray(chiavi) ? chiavi : [chiavi]).every((chiave) => mioPermesso.data?.[chiave] === true)
  }

  const sezioni = navSections
    .filter((section) => section.sempreVisibile || !section.soloSuperAdmin || isSuperAdmin)
    // Entrato in una Struttura, il Super Admin sta operando come quel Cliente: il proprio pannello
    // sparisce dal menu finché non ci torna dalla barra arancione. Due contesti mischiati nella
    // stessa barra sono il modo più facile per fare una cosa nel posto sbagliato.
    .filter((section) => !section.soloSuperAdmin || !(isSuperAdmin && !!strutturaId))
    // Per il SuperAdmin, le sezioni operative restano nascoste finché non seleziona
    // esplicitamente una Struttura (nessuna struttura precaricata all'accesso).
    .filter((section) => section.sempreVisibile || section.soloSuperAdmin || !isSuperAdmin || !!strutturaId)
    .filter((section) => section.sempreVisibile || !section.richiedeGestioneUtenti || isSuperAdmin || mioPermesso.data?.settingUser === true)
    .filter((section) => section.sempreVisibile || haPermesso(section.richiedePermesso))
    .map((section) => ({
      section,
      // Una sezione i cui servizi sono tutti disabilitati (es. "Invii automatici" senza alcun
      // servizio esterno concesso), o le cui voci richiedono tutte un permesso che l'utente non ha
      // (es. "Operativo" per un addetto pulizie), non deve comparire nemmeno come tab.
      voci: section.items.filter(
        (item) => (!item.richiedeServizio || strutturaCorrente?.[item.richiedeServizio] !== false) && haPermesso(item.richiedePermesso),
      ),
    }))
    .filter(({ voci }) => voci.length > 0)

  // Finché la Struttura non è nota i permessi non sono nemmeno stati chiesti, e senza permessi
  // l'unica voce che sopravvive al filtro è "Il mio account" (sempreVisibile, nessun permesso
  // richiesto): decidere in quel momento manderebbe lì sia il redirect di "/" sia le guardie di
  // rotta. Succede al primo accesso da un browser nuovo (il telefono), dove non c'è una struttura
  // ricordata da leggere subito e la lista arriva solo dopo il primo render.
  return { sezioni, caricamento: !isSuperAdmin && (!strutturaId || mioPermesso.isLoading) }
}

export interface VoceProtettaRisultato {
  /** True se l'utente corrente può vedere la voce a questo path (stessa logica del menu). */
  visibile: boolean
  caricamento: boolean
  /** Prima voce di menu disponibile per l'utente, per reindirizzarlo se `visibile` è false. */
  primaVoceDisponibile?: NavItem
}

/**
 * Guardia di rotta: un utente che digita direttamente l'URL di una pagina per cui non ha il
 * permesso (es. `/camere` da addetto pulizie) non deve poterci restare solo perché il menu la
 * nasconde — riusa la stessa lista `useSezioniVisibili` così le due logiche non possono divergere.
 */
export function useVoceProtetta(path: string): VoceProtettaRisultato {
  const { sezioni, caricamento } = useSezioniVisibili()
  const visibile = sezioni.some((s) => s.voci.some((v) => v.path === path))
  return { visibile, caricamento, primaVoceDisponibile: sezioni[0]?.voci[0] }
}
