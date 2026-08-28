import { Navigate, Outlet } from 'react-router-dom'
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

/** Un Cliente senza nessuna Struttura non vede il gestionale: prima deve crearne una. */
function AppGate() {
  const { isSuperAdmin, strutture, loading } = useStruttura()

  if (!isSuperAdmin && !loading && strutture.length === 0) {
    return <OnboardingStrutturaPage />
  }

  return (
    <AppShell>
      <Outlet />
    </AppShell>
  )
}
