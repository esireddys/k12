import { useQuery } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'

export const ownerQueueKey = ['review', 'queue'] as const

/** Questions waiting for an owner, flagged ones first, plus how many questions are in each state. */
export function useOwnerQueue() {
  return useQuery({
    queryKey: ownerQueueKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/admin/review/queue')
      if (error || !data) throw new Error('Could not load the review queue')
      return data
    },
  })
}
