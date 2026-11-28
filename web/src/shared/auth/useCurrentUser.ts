import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

export const currentUserKey = ['me'] as const

/**
 * Who is signed in. A parent with a family has kid profiles; owners are app owners
 * (from the server's configuration), never granted by being a parent.
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/me')
      if (error || !data) throw new Error('Could not load your account')
      return data
    },
    staleTime: 5 * 60_000,
  })
}
