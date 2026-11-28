import { useEffect, useState } from 'react'

/**
 * State that survives a refresh or an accidental swipe back on this device for the rest of the tab's life
 * (sessionStorage). Used for an exam in progress, so a test is not lost. Never holds anything beyond answers.
 */
export function useStoredState<T>(key: string, initial: () => T): [T, (update: (current: T) => T) => void, () => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const raw = sessionStorage.getItem(key)
      return raw ? (JSON.parse(raw) as T) : initial()
    } catch {
      return initial()
    }
  })

  useEffect(() => {
    try {
      sessionStorage.setItem(key, JSON.stringify(value))
    } catch {
      // Storage full or blocked: the exam still works, it just won't survive a refresh.
    }
  }, [key, value])

  const clear = () => {
    try {
      sessionStorage.removeItem(key)
    } catch {
      // nothing to clear
    }
  }

  return [value, (update) => setValue((v) => update(v)), clear]
}
