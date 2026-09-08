import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { LoginPage } from '../pages/LoginPage'
import { DashboardPage } from '../pages/DashboardPage'
import { useStruttura } from '../struttura/StrutturaContext'
import { CalendarioPage } from '../pages/CalendarioPage'
import { CamerePage } from '../pages/CamerePage'
import { PuliziePage } from '../pages/PuliziePage'
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
import { SuperAdminDashboardPage } from '../pages/SuperAdminDashboardPage'
import { SuperAdminClientiPage } from '../pages/SuperAdminClientiPage'
import { SuperAdminImpostazioniPage } from '../pages/SuperAdminImpostazioniPage'
import { SuperAdminBackupPage } from '../pages/SuperAdminBackupPage'
import { ProtectedRoute } from './ProtectedRoute'
import { RouteGuard } from './RouteGuard'
import { useSezioniVisibili } from '../layout/useSezioniVisibili'

/**
 * Il Super Admin senza ancora una Struttura scelta non ha nessun Cruscotto operativo da mostrare:
 * va alla sua dashboard. Una volta scelta una Struttura (tab "Operativo" in alto, il cui primo
 * elemento è proprio "/") deve invece vedere il Cruscotto di quella struttura come chiunque altro.
 *
 * Un utente senza alcun permesso per vedere il Cruscotto (es. addetto pulizie, che vede solo
 * "Pulizie" in menu) non deve nemmeno atterrarci per un istante dopo il login — reindirizzato subito
 * alla prima voce di menu che può effettivamente vedere. Aspetta che i permessi siano arrivati
 * (`caricamento`) prima di decidere, altrimenti un redirect prematuro basato su dati incompleti
 * rimbalzerebbe subito indietro appena i permessi reali risultano più ampi.
 */
function RootRoute() {
  const { isSuperAdmin, strutturaId } = useStruttura()
  const { sezioni, caricamento } = useSezioniVisibili()

  if (isSuperAdmin && !strutturaId) {
    return <Navigate to="/super-admin" replace />
  }

  if (caricamento) {
    return null
  }

  // Non basta guardare sezioni[0]: per un Super Admin la sezione "Super Admin" è sempre la prima
  // (soloSuperAdmin la fa comunque restare in elenco, vedi useSezioniVisibili), quindi sezioni[0]
  // sarebbe sempre "/super-admin" anche quando può benissimo vedere il Cruscotto operativo — bug
  // reale: cliccando la tab "Operativo" (che porta a "/") un Super Admin con Struttura già
  // selezionata veniva rimbalzato indietro su "/super-admin". Il Cruscotto va cercato ovunque tra le
  // sezioni visibili, non assunto assente solo perché non è la prima.
  const cruscottoVisibile = sezioni.some((s) => s.voci.some((v) => v.path === '/'))
  if (cruscottoVisibile) {
    return <DashboardPage />
  }

  const primaVoceDisponibile = sezioni[0]?.voci[0]
  if (primaVoceDisponibile) {
    return <Navigate to={primaVoceDisponibile.path} replace />
  }

  return <DashboardPage />
}

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      { path: '/', element: <RootRoute /> },
      { path: '/calendario', element: <RouteGuard path="/calendario"><CalendarioPage /></RouteGuard> },
      { path: '/camere', element: <RouteGuard path="/camere"><CamerePage /></RouteGuard> },
      { path: '/pulizie', element: <RouteGuard path="/pulizie"><PuliziePage /></RouteGuard> },
      { path: '/tipologie', element: <RouteGuard path="/tipologie"><TipologiePage /></RouteGuard> },
      { path: '/ospiti', element: <RouteGuard path="/ospiti"><OspitiPage /></RouteGuard> },
      { path: '/finanze', element: <Navigate to="/finanze/riepilogo" replace /> },
      { path: '/finanze/riepilogo', element: <RouteGuard path="/finanze/riepilogo"><RiepilogoCassaPage /></RouteGuard> },
      { path: '/finanze/spese', element: <RouteGuard path="/finanze/spese"><SpesePage /></RouteGuard> },
      { path: '/finanze/entrate', element: <RouteGuard path="/finanze/entrate"><EntratePage /></RouteGuard> },
      { path: '/finanze/cauzioni', element: <RouteGuard path="/finanze/cauzioni"><CauzioniPage /></RouteGuard> },
      { path: '/fatturazione', element: <RouteGuard path="/fatturazione"><FatturazionePage /></RouteGuard> },
      { path: '/statistiche', element: <RouteGuard path="/statistiche"><StatistichePage /></RouteGuard> },
      { path: '/polizia-di-stato', element: <RouteGuard path="/polizia-di-stato"><PoliziaPage /></RouteGuard> },
      { path: '/osservatorio', element: <RouteGuard path="/osservatorio"><OsservatorioPage /></RouteGuard> },
      { path: '/paytourist', element: <RouteGuard path="/paytourist"><PayTouristPage /></RouteGuard> },
      { path: '/wubook', element: <RouteGuard path="/wubook"><WubookPage /></RouteGuard> },
      { path: '/utenti', element: <RouteGuard path="/utenti"><UtentiPage /></RouteGuard> },
      { path: '/impostazioni', element: <RouteGuard path="/impostazioni"><ImpostazioniPage /></RouteGuard> },
      { path: '/log', element: <RouteGuard path="/log"><LogPage /></RouteGuard> },
      { path: '/super-admin', element: <RouteGuard path="/super-admin"><SuperAdminDashboardPage /></RouteGuard> },
      { path: '/super-admin/clienti', element: <RouteGuard path="/super-admin/clienti"><SuperAdminClientiPage /></RouteGuard> },
      { path: '/super-admin/backup', element: <RouteGuard path="/super-admin/backup"><SuperAdminBackupPage /></RouteGuard> },
      { path: '/super-admin/impostazioni', element: <RouteGuard path="/super-admin/impostazioni"><SuperAdminImpostazioniPage /></RouteGuard> },
    ],
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
