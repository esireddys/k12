import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'
import type { components } from '../../../shared/api/schema'
import { env } from '../../../shared/config/env'
import type { SetRequest } from '../model/session'

export type StudentQuestion = components['schemas']['StudentQuestion']
export type ScoredResponse = components['schemas']['ScoredResponse']
export type AnswerKind = components['schemas']['AnswerKind']

/** The questions for one session. Kept for the whole session, so a re-render never reshuffles them. */
export function usePracticeSet(request: SetRequest | null) {
  return useQuery({
    queryKey: ['practice', 'set', request],
    enabled: !!request,
    staleTime: Infinity,
    gcTime: 30 * 60_000,
    retry: 1,
    queryFn: async () => {
      const r = request!
      const { data, error } = await api.GET('/api/practice/set', {
        params: {
          query: {
            courseId: r.courseId,
            source: r.source,
            formId: r.formId,
            category: r.category,
            skill: r.skill,
            count: r.count,
            seed: r.seed,
          },
        },
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not load the questions')
      return data
    },
  })
}

/** Scores answers and returns the key. Practice sends one; an exam sends all of them at Finish. */
export function useScore() {
  return useMutation({
    mutationFn: async (responses: { questionId: string; response: string }[]) => {
      const { data, error } = await api.POST('/api/practice/score', { body: { responses } })
      if (error || !data) throw new Error(error?.detail ?? 'Could not check the answers')
      return data
    },
  })
}

/** Saves the course a kid picked to their profile, so it is remembered on every device. */
export function useSaveKidCourse() {
  return useMutation({
    mutationFn: async ({ kidId, courseId }: { kidId: string; courseId: string }) => {
      const { error } = await api.PUT('/api/family/kids/{kidId}/course', { params: { path: { kidId } }, body: { courseId } })
      if (error) throw new Error(error.detail ?? 'Could not save the grade')
    },
  })
}

/** The printed item's picture, served by the API for Live questions only. */
export const questionImageUrl = (questionId: string) => `${env.apiUrl}/api/practice/questions/${questionId}/image`
