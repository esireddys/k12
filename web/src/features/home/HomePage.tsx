import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { api } from '../../shared/api/client'

export function HomePage() {
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
      <Link to="/practice">Try a practice question</Link>
    </main>
  )
}
