import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { DashboardPage } from '../pages/DashboardPage'

const router = createBrowserRouter([
  {
    path: '/',
    element: <DashboardPage />,
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
