import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { useCurrentUser } from '../../../shared/auth/useCurrentUser'

/** Shows its children to app owners only. The API enforces the same rule; this just avoids a dead screen. */
export function OwnerOnly({ children }: { children: ReactNode }) {
  const me = useCurrentUser()
  if (me.isPending) return <p style={{ padding: 24 }}>Checking your account…</p>
  if (me.isError || !me.data?.isOwner)
    return (
      <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 16px' }}>
        <h1>Owners only</h1>
        <p>Content review is for the app's owners.</p>
        {!me.data?.isSignedIn && <Link to="/">Sign in</Link>}
      </main>
    )
  return <>{children}</>
}
