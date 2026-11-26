import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'
import type { AnswerSpec, Difficulty } from '../model/content'
import { ownerQueueKey } from './useOwnerQueue'
import { reviewQuestionKey } from './useReviewQuestion'

type Approve = {
  kind: 'approve'
  version: number
  content: Record<string, unknown>
  answer: AnswerSpec
  difficulty: Difficulty
  note?: string
}
type Reject = { kind: 'reject'; version: number; reason: string }

/** Approves (with the owner's edits) or rejects one question, then refreshes the queue. */
export function useReviewDecision(questionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (decision: Approve | Reject) => {
      const path = { params: { path: { questionId } } }
      const { data, error } =
        decision.kind === 'approve'
          ? await api.POST('/api/admin/review/questions/{questionId}/approve', {
              ...path,
              body: {
                version: decision.version,
                content: decision.content,
                answer: decision.answer,
                difficulty: decision.difficulty,
                note: decision.note || null,
              },
            })
          : await api.POST('/api/admin/review/questions/{questionId}/reject', {
              ...path,
              body: { version: decision.version, reason: decision.reason },
            })
      if (error || !data) throw new Error(error?.detail ?? 'Could not save the decision')
      return data
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ownerQueueKey })
      await queryClient.invalidateQueries({ queryKey: reviewQuestionKey(questionId) })
    },
  })
}
