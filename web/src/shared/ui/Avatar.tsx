import type { AvatarColor } from './avatarColors'

const palette: Record<AvatarColor, { background: string; color: string }> = {
  Orange: { background: '#FFE3C2', color: '#8A4500' },
  Blue: { background: '#D8E6FF', color: '#16449F' },
  Green: { background: '#D3F2DE', color: '#14532D' },
  Purple: { background: '#EDE3FF', color: '#5B21B6' },
  Pink: { background: '#FFE0EE', color: '#9D174D' },
  Teal: { background: '#CDF3EF', color: '#115E59' },
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
