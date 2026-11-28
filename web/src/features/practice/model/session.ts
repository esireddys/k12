import type { components } from '../../../shared/api/schema'

export type SetSource = components['schemas']['SetSource']
export type Mode = 'practice' | 'exam'

/** What a session is made of. Lives in the URL so a refresh keeps the same set (the seed fixes a shuffle). */
export type SetRequest = {
  courseId: string
  source: SetSource
  formId?: string
  category?: number
  skill?: string
  count?: number
  seed?: number
}

export const newSeed = () => Math.floor(Math.random() * 1_000_000_000)

export function sessionUrl(mode: Mode, set: Omit<SetRequest, 'courseId' | 'seed'>): string {
  const params = new URLSearchParams({ mode, source: set.source, seed: String(newSeed()) })
  if (set.formId) params.set('formId', set.formId)
  if (set.category != null) params.set('category', String(set.category))
  if (set.skill) params.set('skill', set.skill)
  if (set.count != null) params.set('count', String(set.count))
  return `/practice/session?${params}`
}

export function readSession(params: URLSearchParams, courseId: string | undefined): { mode: Mode; request: SetRequest } | null {
  const source = params.get('source') as SetSource | null
  if (!courseId || !source || !['Shuffle', 'Year', 'Topic', 'Mistakes', 'PracticeTest', 'Today'].includes(source)) return null
  const num = (name: string) => (params.get(name) != null && !Number.isNaN(Number(params.get(name))) ? Number(params.get(name)) : undefined)
  return {
    mode: params.get('mode') === 'exam' ? 'exam' : 'practice',
    request: {
      courseId,
      source,
      formId: params.get('formId') ?? undefined,
      category: num('category'),
      skill: params.get('skill') ?? undefined,
      count: num('count'),
      seed: num('seed'),
    },
  }
}
