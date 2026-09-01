import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { LoginPage } from '../pages/LoginPage'
import { DashboardPage } from '../pages/DashboardPage'
import { useStruttura } from '../struttura/StrutturaContext'
import { CalendarioPage } from '../pages/CalendarioPage'
import { CamerePage } from '../pages/CamerePage'
import { TipologiePage } from '../pages/TipologiePage'
import { OspitiPage } from '../pages/OspitiPage'
import { FinanzePage } from '../pages/FinanzePage'
import { FatturazionePage } from '../pages/FatturazionePage'
import { PoliziaPage } from '../pages/PoliziaPage'
import { OsservatorioPage } from '../pages/OsservatorioPage'
import { PayTouristPage } from '../pages/PayTouristPage'
import { WubookPage } from '../pages/WubookPage'
import { UtentiPage } from '../pages/UtentiPage'
import { ImpostazioniPage } from '../pages/ImpostazioniPage'
import { LogPage } from '../pages/LogPage'
import { SuperAdminDashboardPage } from '../pages/SuperAdminDashboardPage'
import { ProtectedRoute } from './ProtectedRoute'

/** Il Super Admin non ha una struttura operativa propria: al login va sempre alla sua dashboard. */
function RootRoute() {
  const { isSuperAdmin } = useStruttura()
  return isSuperAdmin ? <Navigate to="/super-admin" replace /> : <DashboardPage />
}

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      { path: '/', element: <RootRoute /> },
      { path: '/calendario', element: <CalendarioPage /> },
      { path: '/camere', element: <CamerePage /> },
      { path: '/tipologie', element: <TipologiePage /> },
      { path: '/ospiti', element: <OspitiPage /> },
      { path: '/finanze', element: <FinanzePage /> },
      { path: '/fatturazione', element: <FatturazionePage /> },
      { path: '/polizia-di-stato', element: <PoliziaPage /> },
      { path: '/osservatorio', element: <OsservatorioPage /> },
      { path: '/paytourist', element: <PayTouristPage /> },
      { path: '/wubook', element: <WubookPage /> },
      { path: '/utenti', element: <UtentiPage /> },
      { path: '/impostazioni', element: <ImpostazioniPage /> },
      { path: '/log', element: <LogPage /> },
      { path: '/super-admin', element: <SuperAdminDashboardPage /> },
    ],
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
