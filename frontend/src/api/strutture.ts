import { useQuery } from '@tanstack/react-query'
import { apiGet } from './client'

export interface StrutturaDto {
  id: string
  clienteId: string
  nome: string
  createdAtUtc: string
}

export function useStrutture(clienteId: string | null) {
  return useQuery({
    queryKey: ['strutture', clienteId ?? 'proprie'],
    queryFn: () => apiGet<StrutturaDto[]>(clienteId ? `/strutture?clienteId=${clienteId}` : '/strutture'),
  })
}
