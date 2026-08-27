import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { StrutturaProvider } from '../struttura/StrutturaContext'
import { AppShell } from '../layout/AppShell'

export function ProtectedRoute() {
  const { sessione } = useAuth()

  if (!sessione) {
    return <Navigate to="/login" replace />
  }

  return (
    <StrutturaProvider>
      <AppShell>
        <Outlet />
      </AppShell>
    </StrutturaProvider>
  )
}
