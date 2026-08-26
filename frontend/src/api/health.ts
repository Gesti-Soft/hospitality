import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface HealthResponse {
  status: string
  version: string
  serverTimeUtc: string
}

export function useHealth() {
  return useQuery({
    queryKey: ['health'],
    queryFn: () => apiGet<HealthResponse>('/health'),
    retry: 1,
  })
}
