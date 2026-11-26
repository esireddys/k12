import type { ButtonHTMLAttributes } from 'react'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' }

/** Shared button with a 44px minimum touch target. */
export function Button({ variant = 'primary', style, ...rest }: Props) {
  const primary = variant === 'primary'
  return (
    <button
      {...rest}
      style={{
        padding: '12px 20px',
        borderRadius: 14,
        border: primary ? 'none' : '2px solid var(--border)',
        background: primary ? 'var(--accent)' : 'var(--surface)',
        color: primary ? '#fff' : 'var(--ink)',
        fontWeight: 800,
        cursor: 'pointer',
        ...style,
      }}
    />
  )
}
