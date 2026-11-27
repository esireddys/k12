/**
 * Text from a printed test, with fractions written "3/4" drawn stacked the way the test prints them.
 * Paragraph breaks in the text are kept.
 */
export function MathText({ text }: { text: string }) {
  const parts = text.split(/(\d+\/\d+)/)
  return (
    <span style={{ whiteSpace: 'pre-line' }}>
      {parts.map((part, i) => {
        const m = /^(\d+)\/(\d+)$/.exec(part)
        if (!m) return part
        return (
          <span key={i} className="fraction" role="math" aria-label={`${m[1]} over ${m[2]}`}>
            <span className="fraction-top">{m[1]}</span>
            <span className="fraction-bottom">{m[2]}</span>
          </span>
        )
      })}
    </span>
  )
}
