import { useEffect, useState } from 'react'
import { getHealth } from './api'

// Placeholder shell. Screens follow the locked wireframes:
// sign in, kid picker, home, topics, tests, question, results, parent, admin.
export default function App() {
  const [apiUp, setApiUp] = useState<boolean | null>(null)

  useEffect(() => {
    getHealth().then(setApiUp)
  }, [])

  return (
    <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 16px' }}>
      <h1 style={{ fontSize: 40, margin: 0 }}>K12 Practice</h1>
      <p style={{ color: 'var(--ink-quiet)', fontSize: 20 }}>Texas STAAR Grade 3 math practice.</p>
      <p>
        API status:{' '}
        <strong>{apiUp === null ? 'checking…' : apiUp ? 'connected' : 'not reachable'}</strong>
      </p>
    </main>
  )
}
