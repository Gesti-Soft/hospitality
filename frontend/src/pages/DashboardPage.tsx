import { useHealth } from '../api/health'

export function DashboardPage() {
  const { data, isLoading, isError, error } = useHealth()

  return (
    <main>
      <h1>GestiSoft Gestionale</h1>
      <section>
        <h2>Stato backend</h2>
        {isLoading && <p>Verifica connessione all'Api...</p>}
        {isError && <p role="alert">Impossibile contattare l'Api: {(error as Error).message}</p>}
        {data && (
          <ul>
            <li>Stato: {data.status}</li>
            <li>Versione: {data.version}</li>
            <li>Ora server (UTC): {data.serverTimeUtc}</li>
          </ul>
        )}
      </section>
    </main>
  )
}
