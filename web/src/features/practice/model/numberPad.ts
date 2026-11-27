import type { AnswerKind } from '../api/usePractice'

/** The extra keys each kind of typed answer needs beyond the digits. Only these can be typed. */
const extraKeys: Record<Exclude<AnswerKind, 'Choice'>, string[]> = {
  Integer: [],
  Decimal: ['.'],
  Fraction: ['/'],
  Money: ['$', '.'],
  Time: [':'],
}

export const maxAnswerLength = 12

export function keysFor(kind: AnswerKind): string[] {
  return kind === 'Choice' ? [] : extraKeys[kind]
}

/** Applies one key press to a typed answer, refusing characters the answer kind cannot hold. */
export function press(value: string, key: string, kind: AnswerKind): string {
  if (key === 'Backspace') return value.slice(0, -1)
  if (key === 'Clear') return ''
  if (value.length >= maxAnswerLength) return value
  const digit = /^[0-9]$/.test(key)
  if (!digit && !keysFor(kind).includes(key)) return value
  if (key === '$' && value.length > 0) return value
  if (['.', '/', ':'].includes(key) && value.includes(key)) return value
  return value + key
}

/** The unit hint shown beside the answer box. */
export const kindHint: Partial<Record<AnswerKind, string>> = {
  Fraction: 'Use / for a fraction, like 3/4',
  Money: 'Dollars and cents, like $2.75',
  Time: 'Hours and minutes, like 3:45',
  Decimal: 'Use . for a decimal',
}
