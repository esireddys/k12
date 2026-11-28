const labels: [string, string][] = [
  ['InAiReview', 'In AI review'],
  ['AwaitingOwner', 'Waiting for you'],
  ['Live', 'Live'],
  ['Rejected', 'Rejected'],
]

/** How far the question bank is through review. */
export function StatusCounts({ counts }: { counts: Record<string, number | string> }) {
  return (
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
      {labels.map(([key, label]) => (
        <div key={key} style={{ background: 'var(--surface)', border: '2px solid var(--border)', borderRadius: 12, padding: '8px 14px' }}>
          <div style={{ fontSize: 24, fontWeight: 800 }}>{Number(counts[key] ?? 0)}</div>
          <div style={{ color: 'var(--ink-quiet)' }}>{label}</div>
        </div>
      ))}
    </div>
  )
}
