import { useQuery } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'

export const reviewQuestionKey = (questionId: string) => ['review', 'question', questionId] as const

export function useReviewQuestion(questionId: string) {
  return useQuery({
    queryKey: reviewQuestionKey(questionId),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/admin/review/questions/{questionId}', {
        params: { path: { questionId } },
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not load the question')
      return data
    },
  })
}
