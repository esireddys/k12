export type CategoryBar = {
  number: number | string
  name: string
  level: 'NotStarted' | 'Learning' | 'Practicing' | 'Mastered'
  answered: number | string
  correct: number | string
  accuracy?: number | string | null
  questionCount?: number | string
}

const levelText: Record<CategoryBar['level'], string> = {
  NotStarted: 'Not started',
  Learning: 'Learning',
  Practicing: 'Practicing',
  Mastered: 'Mastered',
}

const levelColor: Record<CategoryBar['level'], string> = {
  NotStarted: 'var(--border)',
  Learning: 'var(--miss)',
  Practicing: 'var(--accent)',
  Mastered: 'var(--good)',
}

/**
 * Progress in each reporting category of a course, as the state test reports it. The bar shows the share of
 * answers that were right; the word beside it says where the kid stands, so color is never the only signal.
 * Names come from curriculum data.
 */
export function CategoryBars({ categories, showCounts }: { categories: CategoryBar[]; showCounts?: boolean }) {
  return (
    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 14 }}>
      {categories.map((c) => {
        const answered = Number(c.answered)
        const accuracy = c.accuracy == null ? 0 : Number(c.accuracy)
        return (
          <li key={String(c.number)} style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div className="row" style={{ justifyContent: 'space-between', gap: 10, fontSize: 17 }}>
              <span style={{ fontWeight: 700 }}>{c.name}</span>
              <span style={{ fontWeight: 800, color: c.level === 'Mastered' ? 'var(--good-ink)' : 'var(--ink-quiet)', whiteSpace: 'nowrap' }}>
                {levelText[c.level]}
                {answered > 0 && ` · ${accuracy}% right`}
              </span>
            </div>
            <div
              role="meter"
              aria-label={c.name}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={accuracy}
              aria-valuetext={answered > 0 ? `${accuracy}% right, ${levelText[c.level]}` : 'Not started'}
              style={{ height: 12, borderRadius: 6, background: 'var(--bg)', border: '1px solid var(--border)', overflow: 'hidden' }}
            >
              <div style={{ width: `${answered > 0 ? Math.max(accuracy, 3) : 0}%`, height: '100%', background: levelColor[c.level] }} />
            </div>
            {showCounts && (
              <span className="quiet" style={{ fontSize: 14 }}>
                {answered} {answered === 1 ? 'answer' : 'answers'}
                {c.questionCount != null && ` · ${c.questionCount} questions to practice`}
              </span>
            )}
          </li>
        )
      })}
    </ul>
  )
}
