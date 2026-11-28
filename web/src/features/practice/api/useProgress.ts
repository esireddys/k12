import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { api } from '../../../shared/api/client'
import type { components } from '../../../shared/api/schema'
import type { Learner } from '../../../shared/learner/learner'
import { localDate } from '../../../shared/time/localDate'

export type KidSummary = components['schemas']['KidSummary']
export type ScoreResult = components['schemas']['ScoreResult']
type Response = { questionId: string; response: string }

const summaryKey = (kidId: string) => ['progress', 'summary', kidId] as const

/** Streak, stars and how many mistakes are waiting, for a kid's home screen. */
export function useKidSummary(kidId: string | null) {
  return useQuery({
    queryKey: summaryKey(kidId ?? ''),
    enabled: !!kidId,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family/kids/{kidId}/summary', {
        params: { path: { kidId: kidId! }, query: { localDate: localDate() } },
      })
      if (error || !data) throw new Error('Could not load your progress')
      return data
    },
  })
}

/** A set of the kid's missed questions. Kept for the session like any other set. */
export function useMistakesSet(kidId: string | null, key: string) {
  return useQuery({
    queryKey: ['practice', 'mistakes', kidId, key],
    enabled: !!kidId,
    staleTime: Infinity,
    gcTime: 30 * 60_000,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family/kids/{kidId}/mistakes', { params: { path: { kidId: kidId! } } })
      if (error || !data) throw new Error(error?.detail ?? 'Could not load your mistakes')
      return data
    },
  })
}

/** A kid's "Today's practice": a few of their mistakes, then their weakest area, then a little of everything. */
export function useTodaySet(kidId: string | null, request: { count?: number; seed?: number } | null) {
  return useQuery({
    queryKey: ['practice', 'today', kidId, request],
    enabled: !!kidId && !!request,
    staleTime: Infinity,
    gcTime: 30 * 60_000,
    retry: 1,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family/kids/{kidId}/today', {
        params: { path: { kidId: kidId! }, query: { count: request!.count, seed: request!.seed } },
      })
      if (error || !data) throw new Error(error?.detail ?? "Could not load today's practice")
      return data
    },
  })
}

/**
 * Scores answers for whoever is practicing. A kid's answers go to their history and progress; a guest's are only
 * scored, and nothing is saved. Both get the same result back, so the screens do not care which.
 */
export function useSessionScoring({ learner, mode, source, title, sessionId: keptId }: {
  learner: Learner
  mode: 'practice' | 'exam'
  source: string
  title: string
  /** The set's id when it was started earlier (a practice set picked up after a refresh). */
  sessionId?: string
}) {
  const queryClient = useQueryClient()
  const [newId] = useState(() => crypto.randomUUID())
  const sessionId = keptId ?? newId
  // Time since the last batch, so each answer's time is counted once.
  const since = useRef<number | null>(null)
  if (since.current === null) since.current = Date.now()
  const kidId = learner.kind === 'kid' ? learner.kidId : null

  const score = useMutation({
    mutationFn: async ({ responses, finish = false, startedAt }: { responses: Response[]; finish?: boolean; startedAt?: number }) => {
      const now = Date.now()
      const elapsedSeconds = Math.max(0, Math.round((now - (startedAt ?? since.current ?? now)) / 1000))
      since.current = now
      if (!kidId) {
        const { data, error } = await api.POST('/api/practice/score', { body: { responses } })
        if (error || !data) throw new Error(error?.detail ?? 'Could not check the answers')
        return data
      }
      const { data, error } = await api.POST('/api/family/kids/{kidId}/answers', {
        params: { path: { kidId } },
        body: {
          sessionId,
          mode: mode === 'exam' ? 'Exam' : 'Practice',
          source,
          title,
          localDate: localDate(),
          elapsedSeconds,
          responses,
          finish,
        },
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not check the answers')
      return data.score
    },
    onSuccess: () => kidId && queryClient.invalidateQueries({ queryKey: summaryKey(kidId) }),
  })

  /** Ends a practice set when the kid sees their results. Nothing to do for a guest or an empty set. */
  const finish = useMutation({
    mutationFn: async () => {
      if (!kidId) return
      // A set where nothing was answered was never saved: the server answers 404, which is fine.
      await api.POST('/api/family/kids/{kidId}/sessions/{sessionId}/finish', { params: { path: { kidId, sessionId } } })
    },
  })

  return { score, finish }
}
