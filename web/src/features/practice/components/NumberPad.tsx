import { useEffect } from 'react'
import { Icon } from '../../../shared/ui/Icon'
import type { AnswerKind } from '../api/usePractice'
import { keysFor, press } from '../model/numberPad'

const key = {
  height: 64,
  borderRadius: 16,
  border: '2px solid var(--border)',
  background: 'var(--surface)',
  fontSize: 26,
  fontWeight: 800,
  color: 'var(--ink)',
  cursor: 'pointer',
} as const

/**
 * The app's own number pad, so the tablet keyboard never covers the question and only valid characters
 * can be typed. A hardware keyboard works too.
 */
export function NumberPad({
  kind,
  value,
  onChange,
  disabled,
}: {
  kind: AnswerKind
  value: string
  onChange: (next: string) => void
  disabled?: boolean
}) {
  const extras = keysFor(kind)

  useEffect(() => {
    if (disabled) return
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey) return
      const target = e.target as HTMLElement | null
      if (target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) return
      const next = press(value, e.key === 'Delete' ? 'Clear' : e.key, kind)
      if (next !== value) {
        e.preventDefault()
        onChange(next)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [value, kind, onChange, disabled])

  const tap = (k: string) => onChange(press(value, k, kind))

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 72px)', gap: 10 }} aria-label="Number pad">
      {['7', '8', '9', '4', '5', '6', '1', '2', '3'].map((d) => (
        <button key={d} type="button" style={key} onClick={() => tap(d)} disabled={disabled}>
          {d}
        </button>
      ))}
      {extras.includes('.') ? (
        <button type="button" style={key} onClick={() => tap('.')} disabled={disabled} aria-label="Decimal point">
          .
        </button>
      ) : (
        <span />
      )}
      <button type="button" style={key} onClick={() => tap('0')} disabled={disabled}>
        0
      </button>
      <button type="button" style={{ ...key, display: 'flex', alignItems: 'center', justifyContent: 'center' }} onClick={() => tap('Backspace')} disabled={disabled} aria-label="Delete">
        <Icon name="erase" size={26} />
      </button>
      {extras
        .filter((k) => k !== '.')
        .map((k) => (
          <button key={k} type="button" style={{ ...key, background: 'var(--accent-soft)' }} onClick={() => tap(k)} disabled={disabled} aria-label={k === '/' ? 'Fraction bar' : k === ':' ? 'Colon' : 'Dollar sign'}>
            {k}
          </button>
        ))}
    </div>
  )
}
