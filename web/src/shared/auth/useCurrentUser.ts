import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

/** Who is signed in and whether they are an app owner. Owners are app owners, never parents. */
export function useCurrentUser() {
  return useQuery({
    queryKey: ['me'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/me')
      if (error || !data) throw new Error('Could not load your account')
      return data
    },
    staleTime: 5 * 60_000,
  })
}
