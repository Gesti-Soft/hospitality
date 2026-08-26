const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new ApiError(response.status, `Richiesta a ${path} fallita (${response.status})`)
  }

  return (await response.json()) as T
}
