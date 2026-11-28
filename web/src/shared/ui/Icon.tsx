const paths = {
  back: <path d="M15 18l-6-6 6-6" />,
  close: <path d="M6 6l12 12M18 6L6 18" />,
  lock: (
    <>
      <rect x="4" y="11" width="16" height="10" rx="2" />
      <path d="M8 11V7a4 4 0 0 1 8 0v4" />
    </>
  ),
  plus: <path d="M12 5v14M5 12h14" />,
  grid: (
    <>
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </>
  ),
  shuffle: <path d="M16 3h5v5M4 20 21 3M21 16v5h-5M15 15l6 6M4 4l5 5" />,
  clock: (
    <>
      <circle cx="12" cy="13" r="8" />
      <path d="M12 9v4l2 2M9 2h6" />
    </>
  ),
  calendar: (
    <>
      <rect x="3" y="4" width="18" height="17" rx="2" />
      <path d="M3 9h18M8 2v4M16 2v4" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v5M12 16v.5" />
    </>
  ),
  flag: <path d="M5 21V4h11l-2 4 2 4H5" />,
  retry: (
    <>
      <path d="M3 12a9 9 0 1 0 3-6.7L3 8" />
      <path d="M3 3v5h5" />
    </>
  ),
  chart: <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />,
  erase: (
    <>
      <path d="M21 5H8l-6 7 6 7h13z" />
      <path d="M12 9l6 6M18 9l-6 6" />
    </>
  ),
  book: (
    <>
      <path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z" />
      <path d="M4 21V5M8 7h7" />
    </>
  ),
  zoom: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="M20 20l-4-4M11 8v6M8 11h6" />
    </>
  ),
  medal: (
    <>
      <circle cx="12" cy="15" r="6" />
      <path d="M8 3h8l-2 6h-4zM12 12v3l2 1" />
    </>
  ),
  check: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  star: <path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z" />,
  // Filled versions mark an "on" state (picked, flagged, earned). Same shape, so the icon set stays one family.
  'star-filled': <path d="M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z" fill="currentColor" />,
  'flag-filled': <path d="M5 21V4h11l-2 4 2 4H5" fill="currentColor" />,
  sun: (
    <>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
    </>
  ),
} as const

export type IconName = keyof typeof paths

/**
 * The app's one icon set: rounded line icons on a 24 px grid, 2.2 px stroke, round caps and joins.
 * Use a `-filled` name for an "on" state. Decorative unless a label is given.
 * Misses never get an X or a cross; use words ("Not yet") with the coral colour instead.
 */
export function Icon({ name, size = 22, color = 'currentColor', label }: { name: IconName; size?: number; color?: string; label?: string }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={2.2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden={label ? undefined : true}
      role={label ? 'img' : undefined}
      aria-label={label}
    >
      {paths[name]}
    </svg>
  )
}
