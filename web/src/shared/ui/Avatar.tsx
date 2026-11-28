import type { AvatarColor } from './avatarColors'

const palette: Record<AvatarColor, { background: string; color: string }> = {
  Orange: { background: 'var(--avatar-orange-bg)', color: 'var(--avatar-orange-ink)' },
  Blue: { background: 'var(--avatar-blue-bg)', color: 'var(--avatar-blue-ink)' },
  Green: { background: 'var(--avatar-green-bg)', color: 'var(--avatar-green-ink)' },
  Purple: { background: 'var(--avatar-purple-bg)', color: 'var(--avatar-purple-ink)' },
  Pink: { background: 'var(--avatar-pink-bg)', color: 'var(--avatar-pink-ink)' },
  Teal: { background: 'var(--avatar-teal-bg)', color: 'var(--avatar-teal-ink)' },
}

/** A kid's picture: a coloured initial. Kids never get a photo. */
export function Avatar({ name, color, size = 56 }: { name: string; color: AvatarColor; size?: number }) {
  const look = palette[color] ?? palette.Blue
  const initial = name.trim().charAt(0).toUpperCase() || '?'
  return (
    <span
      aria-hidden="true"
      style={{
        width: size,
        height: size,
        borderRadius: size / 2,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'var(--font-display)',
        fontWeight: 800,
        fontSize: size * 0.42,
        flexShrink: 0,
        ...look,
      }}
    >
      {initial}
    </span>
  )
}
