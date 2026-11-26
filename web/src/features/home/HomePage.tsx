import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api } from '../../shared/api/client'
import { useCurrentUser } from '../../shared/auth/useCurrentUser'

export function HomePage() {
  const me = useCurrentUser()
  const health = useQuery({
    queryKey: ['health'],
    queryFn: async () => (await api.GET('/health')).response.ok,
  })

  return (
    <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 16px' }}>
      <h1 style={{ fontSize: 40, margin: 0 }}>K12 Practice</h1>
      <p style={{ color: 'var(--ink-quiet)', fontSize: 20 }}>Math practice, one question at a time.</p>
      <p>
        API status: <strong>{health.isPending ? 'checking…' : health.data ? 'connected' : 'not reachable'}</strong>
      </p>
      <p style={{ display: 'flex', gap: 24 }}>
        <Link to="/practice">Try a practice question</Link>
        {me.data?.isOwner && <Link to="/owner/review">Content review (owners)</Link>}
      </p>
    </main>
  )
}
