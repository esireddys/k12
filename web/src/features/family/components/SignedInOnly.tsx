import { useEffect, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useCurrentUser } from '../../../shared/auth/useCurrentUser'

/** Parent screens: anyone without a family session goes back to the welcome screen. The API enforces the same. */
export function SignedInOnly({ children }: { children: ReactNode }) {
  const me = useCurrentUser()
  const navigate = useNavigate()
  const allowed = me.data?.hasFamily === true

  useEffect(() => {
    if (me.isSuccess && !allowed) navigate('/', { replace: true })
  }, [me.isSuccess, allowed, navigate])

  if (!allowed) return <p style={{ padding: 24 }}>{me.isError ? 'Could not reach the server.' : 'Checking your account…'}</p>
  return <>{children}</>
}
