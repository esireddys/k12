import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'
import type { AnswerSpec } from '../model/content'
import { reviewQuestionKey } from './useReviewQuestion'

export type BrowseFilters = { courseId?: string; year?: number; skill?: string; hasExplanation?: boolean; reported?: boolean }

const browseKey = ['review', 'browse'] as const

/** Live questions an owner can open and fix, with filters. Reported ones come first. */
export function useBrowseQuestions(filters: BrowseFilters) {
  return useQuery({
    queryKey: [...browseKey, filters],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/admin/review/questions', { params: { query: { ...filters, take: 500 } } })
      if (error || !data) throw new Error(error?.detail ?? 'Could not load the questions')
      return data
    },
  })
}

/** Open reports from families, oldest first. */
export function useQuestionReports() {
  return useQuery({
    queryKey: ['review', 'reports'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/admin/review/reports')
      if (error || !data) throw new Error('Could not load the reports')
      return data.reports
    },
  })
}

/** Saves an owner's correction to a Live question. A released question stays Live; its open reports are closed. */
export function useReviseQuestion(questionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: { version: number; content: Record<string, unknown>; answer: AnswerSpec }) => {
      const { data, error } = await api.POST('/api/admin/review/questions/{questionId}/revise', {
        params: { path: { questionId } },
        body,
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not save the change')
      return data
    },
    onSuccess: () => invalidate(queryClient, questionId),
  })
}

/** Marks a question's open reports as dealt with when nothing needed to change. */
export function useCloseReports(questionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST('/api/admin/review/questions/{questionId}/reports/close', {
        params: { path: { questionId } },
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not close the reports')
      return data
    },
    onSuccess: () => invalidate(queryClient, questionId),
  })
}

async function invalidate(queryClient: ReturnType<typeof useQueryClient>, questionId: string) {
  await queryClient.invalidateQueries({ queryKey: browseKey })
  await queryClient.invalidateQueries({ queryKey: ['review', 'reports'] })
  await queryClient.invalidateQueries({ queryKey: reviewQuestionKey(questionId) })
}
