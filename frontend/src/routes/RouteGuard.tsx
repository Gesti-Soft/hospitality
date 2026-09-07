import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useVoceProtetta } from '../layout/useSezioniVisibili'

/**
 * Blocca l'accesso diretto via URL a una pagina per cui l'utente non ha il permesso — il menu la
 * nasconde già, ma senza questa guardia digitare l'indirizzo a mano bypassava il controllo.
 */
export function RouteGuard({ path, children }: { path: string; children: ReactNode }) {
  const { visibile, caricamento, primaVoceDisponibile } = useVoceProtetta(path)

  if (caricamento) {
    return null
  }

  if (!visibile) {
    return <Navigate to={primaVoceDisponibile?.path ?? '/'} replace />
  }

  return children
}
