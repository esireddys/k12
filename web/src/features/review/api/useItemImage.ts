import { useQuery } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'

/**
 * The picture of the printed item, fetched through the API client so it carries the owner's sign-in
 * once real sign-in exists.
 */
export function useItemImage(path: string | undefined) {
  return useQuery({
    queryKey: ['review', 'image', path],
    enabled: !!path,
    staleTime: Infinity,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/admin/review/images/{path}', {
        params: { path: { path: path! } },
        parseAs: 'blob',
      })
      if (error || !data) throw new Error('Picture not found')
      return data as Blob
    },
  })
}
