import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'dark' | 'ghost'
type Props = ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; size?: 'md' | 'lg' }

const looks: Record<Variant, { background: string; color: string; border: string }> = {
  primary: { background: 'var(--accent)', color: '#fff', border: '2px solid var(--accent)' },
  secondary: { background: 'var(--surface)', color: 'var(--ink)', border: '2px solid var(--border)' },
  dark: { background: 'var(--surface)', color: 'var(--ink)', border: '2px solid var(--ink)' },
  ghost: { background: 'transparent', color: 'var(--accent)', border: '2px solid transparent' },
}

/** Shared button with a 44px minimum touch target (56px for large). */
export function Button({ variant = 'primary', size = 'md', style, disabled, ...rest }: Props) {
  const look = looks[variant]
  return (
    <button
      {...rest}
      disabled={disabled}
      style={{
        padding: size === 'lg' ? '16px 28px' : '12px 20px',
        minHeight: size === 'lg' ? 56 : 44,
        borderRadius: 14,
        ...look,
        fontWeight: 800,
        fontSize: size === 'lg' ? 20 : 16,
        cursor: disabled ? 'default' : 'pointer',
        opacity: disabled ? 0.55 : 1,
        ...style,
      }}
    />
  )
}
