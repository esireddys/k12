import { useMutation } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'

/** Sends one response to the API for scoring. The correct answer never comes to the device. */
export function useCheckAnswer(questionId: string) {
  return useMutation({
    mutationFn: async (response: string) => {
      const { data, error } = await api.POST('/api/practice/questions/{questionId}/check', {
        params: { path: { questionId } },
        body: { response },
      })
      if (error) throw new Error(error.detail ?? 'Could not check the answer')
      return data
    },
  })
}
