/** What the time line says: whole minutes, or "under 1 min" for a quick set. */
export const timeText = (minutes: number) => (minutes < 1 ? 'under 1 min' : `${minutes} min`)

/**
 * The headline: what the kid did before any percentage, and the next step. Praise only for a good score, never
 * an exclamation mark for a low one. A test with most questions left blank says so instead of reading as failed.
 */
export function resultMessage({ total, answered, correct }: { total: number; answered: number; correct: number }): string {
  const percent = total ? Math.round((correct / total) * 100) : 0
  const missed = answered - correct
  if (answered === 0) return `You didn't answer any of the ${total} questions this time.`
  const what = answered < total ? `You answered ${answered} of ${total}.` : percent >= 80 ? 'Great work!' : percent >= 60 ? 'Nice effort!' : `You answered all ${total}.`
  const next = missed > 0 ? ` Let's fix the ${missed} you missed.` : answered < total ? ' Every one you answered was right.' : ''
  return what + next
}
