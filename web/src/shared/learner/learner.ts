import { useSyncExternalStore } from 'react'

/**
 * Who is practicing on this device right now: a guest, or one of the signed-in parent's kids.
 * Kept in the browser only, so a refresh keeps the same learner. Holds no personal data beyond the
 * kid's first name and picture colour, which the parent entered.
 */
export type Learner =
  | { kind: 'guest'; courseId?: string }
  | { kind: 'kid'; kidId: string; name: string; avatar: string; courseId?: string }

const key = 'k12.learner'
const listeners = new Set<() => void>()
let cached: { raw: string | null; value: Learner | null } = { raw: null, value: null }

function read(): Learner | null {
  let raw: string | null = null
  try {
    raw = localStorage.getItem(key)
  } catch {
    return cached.value
  }
  if (raw === cached.raw) return cached.value
  let value: Learner | null = null
  try {
    value = raw ? (JSON.parse(raw) as Learner) : null
  } catch {
    value = null
  }
  cached = { raw, value }
  return value
}

function write(value: Learner | null) {
  try {
    if (value) localStorage.setItem(key, JSON.stringify(value))
    else localStorage.removeItem(key)
  } catch {
    cached = { raw: value ? JSON.stringify(value) : null, value }
  }
  listeners.forEach((l) => l())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  const onStorage = (e: StorageEvent) => e.key === key && listener()
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}

export function useLearner(): Learner | null {
  return useSyncExternalStore(subscribe, read, () => null)
}

export const learnerStore = {
  get: read,
  set: write,
  clear: () => write(null),
  setCourse(courseId: string) {
    const current = read()
    if (current) write({ ...current, courseId })
  },
}
