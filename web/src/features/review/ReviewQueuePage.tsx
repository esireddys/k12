import { useState } from 'react'
import { Link } from 'react-router'
import { useOwnerQueue } from './api/useOwnerQueue'
import { OwnerNav } from './components/OwnerNav'
import { OwnerOnly } from './components/OwnerOnly'
import { StatusCounts } from './components/StatusCounts'
import { difficultyLabel, sourceLine, type OwnerQueueItem } from './model/content'

type Filter = 'all' | 'flagged' | 'clean'

function QueueRow({ item }: { item: OwnerQueueItem }) {
  const flagged = item.verdict === 'NeedsAttention'
  return (
    <li>
      <Link
        to={`/owner/review/${item.id}`}
        style={{
          display: 'grid',
          gap: 4,
          padding: 14,
          borderRadius: 14,
          border: `2px solid ${flagged ? 'var(--danger-line)' : 'var(--border)'}`,
          background: 'var(--surface)',
          color: 'var(--ink)',
          textDecoration: 'none',
        }}
      >
        <strong>{sourceLine(item.source, item.skillCodes)}</strong>
        <span style={{ color: flagged ? 'var(--danger-ink)' : 'var(--good-ink)' }}>
          {flagged ? `Needs attention · ${item.problemCount} problem(s), ${item.warningCount} check(s)` : 'Looks good'}
          {item.answerKeyAgrees === false && ' · key disagrees'}
          {item.suggestedDifficulty && item.suggestedDifficulty !== item.difficulty &&
            ` · suggests ${difficultyLabel[item.suggestedDifficulty]}`}
        </span>
        {item.summary && <span style={{ color: 'var(--ink-quiet)' }}>{item.summary}</span>}
      </Link>
    </li>
  )
}

function Queue() {
  const queue = useOwnerQueue()
  const [filter, setFilter] = useState<Filter>('all')

  if (queue.isPending) return <p>Loading the queue…</p>
  if (queue.isError) return <p>{queue.error.message}</p>

  const items = queue.data.items.filter((i) =>
    filter === 'all' ? true : filter === 'flagged' ? i.verdict === 'NeedsAttention' : i.verdict !== 'NeedsAttention',
  )

  return (
    <>
      <StatusCounts counts={queue.data.counts} />
      <div role="tablist" style={{ display: 'flex', gap: 8 }}>
        {(['all', 'flagged', 'clean'] as Filter[]).map((f) => (
          <button
            key={f}
            role="tab"
            aria-selected={filter === f}
            onClick={() => setFilter(f)}
            style={{
              borderRadius: 999,
              padding: '6px 16px',
              border: '2px solid var(--border)',
              background: filter === f ? 'var(--accent)' : 'var(--surface)',
              color: filter === f ? 'var(--on-accent)' : 'var(--ink)',
              fontWeight: 700,
            }}
          >
            {f === 'all' ? 'All' : f === 'flagged' ? 'Needs attention' : 'Looks good'}
          </button>
        ))}
      </div>
      {items.length === 0 ? (
        <p>
          Nothing is waiting for an owner. Questions written by the generator land here after the AI reviewer checks them. To fix a
          question kids already see, open <Link to="/owner/questions">All questions</Link>.
        </p>
      ) : (
        <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'grid', gap: 10 }}>
          {items.map((item) => (
            <QueueRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </>
  )
}

/** Owners work through questions the AI reviewer has checked. Flagged ones come first. */
export function ReviewQueuePage() {
  return (
    <OwnerOnly>
      <main style={{ maxWidth: 960, margin: '0 auto', padding: '32px 16px', display: 'grid', gap: 20 }}>
        <OwnerNav />
        <h1 style={{ margin: 0 }}>Content review</h1>
        <Queue />
      </main>
    </OwnerOnly>
  )
}
