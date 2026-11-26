import type { QuestionForReview } from '../model/content'

const tone = {
  Problem: { bg: '#fde8e8', ink: '#8a1c1c', label: 'Problem' },
  Warning: { bg: '#fff4d6', ink: '#7a5200', label: 'Check' },
  Info: { bg: '#e8f0fe', ink: '#1f3f8a', label: 'Note' },
} as const

/** The AI reviewer's call and findings, problems first. */
export function Findings({ review, answer }: { review: QuestionForReview['aiReview']; answer: string }) {
  if (!review) return <p>The AI reviewer has not looked at this version yet.</p>
  const order = { Problem: 0, Warning: 1, Info: 2 }
  const findings = [...review.findings].sort((a, b) => order[a.severity] - order[b.severity])
  const flagged = review.verdict === 'NeedsAttention'

  return (
    <section style={{ display: 'grid', gap: 8 }}>
      <div
        style={{
          padding: 12,
          borderRadius: 12,
          background: flagged ? '#fde8e8' : '#e6f6ec',
          color: flagged ? '#8a1c1c' : '#1b5e33',
          fontWeight: 700,
        }}
      >
        {flagged ? 'AI reviewer: needs attention' : 'AI reviewer: looks good'}
        <div style={{ fontWeight: 400, marginTop: 4 }}>{review.summary}</div>
      </div>
      <div>
        Answer key <strong>{answer}</strong>, reviewer solved <strong>{review.solvedAnswer}</strong>{' '}
        {review.answerKeyAgrees ? '(agrees)' : '(DISAGREES)'}
      </div>
      {findings.length > 0 && (
        <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'grid', gap: 6 }}>
          {findings.map((f, i) => (
            <li key={i} style={{ background: tone[f.severity].bg, color: tone[f.severity].ink, padding: 10, borderRadius: 10 }}>
              <strong>{tone[f.severity].label}</strong> · {f.area.replace('_', ' ')}: {f.message}
            </li>
          ))}
        </ul>
      )}
      <small style={{ color: 'var(--ink-quiet)' }}>
        {review.reviewer} · {new Date(review.reviewedAt).toLocaleString()}
      </small>
    </section>
  )
}
