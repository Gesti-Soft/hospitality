import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { LoginPage } from '../pages/LoginPage'
import { DashboardPage } from '../pages/DashboardPage'
import { useStruttura } from '../struttura/StrutturaContext'
import { CalendarioPage } from '../pages/CalendarioPage'
import { CamerePage } from '../pages/CamerePage'
import { TipologiePage } from '../pages/TipologiePage'
import { OspitiPage } from '../pages/OspitiPage'
import { RiepilogoCassaPage } from '../pages/RiepilogoCassaPage'
import { SpesePage } from '../pages/SpesePage'
import { EntratePage } from '../pages/EntratePage'
import { CauzioniPage } from '../pages/CauzioniPage'
import { FatturazionePage } from '../pages/FatturazionePage'
import { PoliziaPage } from '../pages/PoliziaPage'
import { OsservatorioPage } from '../pages/OsservatorioPage'
import { PayTouristPage } from '../pages/PayTouristPage'
import { WubookPage } from '../pages/WubookPage'
import { UtentiPage } from '../pages/UtentiPage'
import { ImpostazioniPage } from '../pages/ImpostazioniPage'
import { LogPage } from '../pages/LogPage'
import { StatistichePage } from '../pages/StatistichePage'
import { StatisticheSuperAdminPage } from '../pages/StatisticheSuperAdminPage'
import { SuperAdminDashboardPage } from '../pages/SuperAdminDashboardPage'
import { ProtectedRoute } from './ProtectedRoute'

/**
 * Il Super Admin senza ancora una Struttura scelta non ha nessun Cruscotto operativo da mostrare:
 * va alla sua dashboard. Una volta scelta una Struttura (tab "Operativo" in alto, il cui primo
 * elemento è proprio "/") deve invece vedere il Cruscotto di quella struttura come chiunque altro.
 */
function RootRoute() {
  const { isSuperAdmin, strutturaId } = useStruttura()
  return isSuperAdmin && !strutturaId ? <Navigate to="/super-admin" replace /> : <DashboardPage />
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
      { path: '/finanze', element: <Navigate to="/finanze/riepilogo" replace /> },
      { path: '/finanze/riepilogo', element: <RiepilogoCassaPage /> },
      { path: '/finanze/spese', element: <SpesePage /> },
      { path: '/finanze/entrate', element: <EntratePage /> },
      { path: '/finanze/cauzioni', element: <CauzioniPage /> },
      { path: '/fatturazione', element: <FatturazionePage /> },
      { path: '/statistiche', element: <StatistichePage /> },
      { path: '/polizia-di-stato', element: <PoliziaPage /> },
      { path: '/osservatorio', element: <OsservatorioPage /> },
      { path: '/paytourist', element: <PayTouristPage /> },
      { path: '/wubook', element: <WubookPage /> },
      { path: '/utenti', element: <UtentiPage /> },
      { path: '/impostazioni', element: <ImpostazioniPage /> },
      { path: '/log', element: <LogPage /> },
      { path: '/super-admin', element: <SuperAdminDashboardPage /> },
      { path: '/super-admin/statistiche', element: <StatisticheSuperAdminPage /> },
    ],
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
