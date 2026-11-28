/**
 * "Pick up where you left off": the set a learner had open in this tab, so the home screen can offer it.
 * Kept in sessionStorage next to the set's answers (see storedState), so it never outlives the tab and holds
 * nothing but the set's link and how far along it is.
 */
import type { Learner } from '../../../shared/learner/learner'

export type ResumePoint = { url: string; title: string; mode: 'practice' | 'exam'; done: number; total: number }

const key = (learnerKey: string) => `k12.resume:${learnerKey}`

export function readResume(learnerKey: string): ResumePoint | null {
  try {
    const raw = sessionStorage.getItem(key(learnerKey))
    return raw ? (JSON.parse(raw) as ResumePoint) : null
  } catch {
    return null
  }
}

export function saveResume(learnerKey: string, point: ResumePoint) {
  try {
    sessionStorage.setItem(key(learnerKey), JSON.stringify(point))
  } catch {
    // Storage blocked: there is simply no resume card.
  }
}

export function clearResume(learnerKey: string, url?: string) {
  try {
    // Only clear the pointer when it still points at this set, so finishing an old set keeps a newer one.
    if (url && readResume(learnerKey)?.url !== url) return
    sessionStorage.removeItem(key(learnerKey))
  } catch {
    // nothing to clear
  }
}

/** Whose sets these are on this device: one kid, or the guest. */
export const learnerKey = (learner: Learner) => (learner.kind === 'kid' ? learner.kidId : 'guest')
