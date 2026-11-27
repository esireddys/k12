import { useEffect, useState } from 'react'
import { Icon } from '../../../shared/ui/Icon'

const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'del'] as const

/**
 * Four dots and a big keypad for the parent PIN. Calls onComplete when four digits are in, then clears itself when
 * `resetKey` changes (for example after a wrong PIN). Typing on a keyboard works too.
 */
export function PinPad({ onComplete, disabled, resetKey }: { onComplete: (pin: string) => void; disabled?: boolean; resetKey?: unknown }) {
  const [pin, setPin] = useState('')

  useEffect(() => setPin(''), [resetKey])

  const press = (key: string) => {
    if (disabled) return
    if (key === 'del') return setPin((p) => p.slice(0, -1))
    if (pin.length >= 4) return
    const next = pin + key
    setPin(next)
    if (next.length === 4) onComplete(next)
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 20 }}>
      <div className="row" style={{ gap: 16 }} aria-label={`${pin.length} of 4 digits entered`} role="status">
        {[0, 1, 2, 3].map((i) => (
          <span
            key={i}
            style={{
              width: 22,
              height: 22,
              borderRadius: 11,
              boxSizing: 'border-box',
              background: i < pin.length ? 'var(--accent)' : 'transparent',
              border: i < pin.length ? 'none' : '3px solid var(--border-strong)',
            }}
          />
        ))}
      </div>
      {/* A real input keeps the keyboard and screen readers working; the pad below is for touch. */}
      <input
        className="visually-hidden"
        aria-label="Parent PIN"
        inputMode="numeric"
        autoComplete="off"
        value={pin}
        disabled={disabled}
        autoFocus
        onChange={(e) => {
          const digits = e.target.value.replace(/\D/g, '').slice(0, 4)
          setPin(digits)
          if (digits.length === 4) onComplete(digits)
        }}
      />
      <div style={{ width: 300, display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 12 }}>
        {keys.map((key, i) =>
          key === '' ? (
            <span key={i} />
          ) : (
            <button
              key={i}
              type="button"
              onClick={() => press(key)}
              disabled={disabled}
              aria-label={key === 'del' ? 'Delete' : key}
              style={{
                height: 64,
                borderRadius: 16,
                border: '2px solid var(--border)',
                background: 'var(--surface)',
                fontSize: 26,
                fontWeight: 800,
                color: 'var(--ink)',
                cursor: disabled ? 'default' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              {key === 'del' ? <Icon name="erase" size={26} /> : key}
            </button>
          ),
        )}
      </div>
    </div>
  )
}
