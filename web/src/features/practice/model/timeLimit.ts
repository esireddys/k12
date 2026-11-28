/** "3 h 58 min left" or "12 min left" for a countdown, from whole minutes left (rounded up). */
export function timeLeftText(minutes: number) {
  if (minutes >= 60) return `${Math.floor(minutes / 60)} h${minutes % 60 ? ` ${minutes % 60} min` : ''} left`
  return minutes <= 1 ? 'Less than a minute left' : `${minutes} min left`
}

/** "4 hours" or "90 minutes" for a test's time limit. */
export function timeLimitText(minutes: number) {
  if (minutes % 60 === 0) return minutes === 60 ? '1 hour' : `${minutes / 60} hours`
  return `${minutes} minutes`
}
