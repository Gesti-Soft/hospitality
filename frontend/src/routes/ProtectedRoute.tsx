import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { StrutturaProvider, useStruttura } from '../struttura/StrutturaContext'
import { AppShell } from '../layout/AppShell'
import { OnboardingStrutturaPage } from '../pages/OnboardingStrutturaPage'

export function ProtectedRoute() {
  const { sessione } = useAuth()

  if (!sessione) {
    return <Navigate to="/login" replace />
  }

  return (
    <StrutturaProvider>
      <AppGate />
    </StrutturaProvider>
  )
}

/** Un Cliente senza nessuna Struttura non vede il gestionale: solo il Super Admin può crearne una. */
function AppGate() {
  const { isSuperAdmin, strutture, loading } = useStruttura()
  const { pathname } = useLocation()

  // "Il mio account" resta raggiungibile anche senza strutture: chi è appena stato creato e non è
  // ancora stato assegnato a nulla deve comunque poter cambiare la password iniziale ricevuta e
  // attivare la verifica in due passaggi, invece di restare fermo su una pagina di attesa.
  if (!isSuperAdmin && !loading && strutture.length === 0 && pathname !== '/mio-account') {
    return <OnboardingStrutturaPage />
  }

  return (
    <AppShell>
      <Outlet />
    </AppShell>
  )
}
