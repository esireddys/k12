import { useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { isOpen, useParentGate } from './api/useParentArea'
import { SignedInOnly } from './components/SignedInOnly'
import { UnlockScreen } from './components/ParentGate'

/** Only same-app paths, so a link cannot send the parent somewhere else after the PIN. */
const safeNext = (next: string | null) => (next && next.startsWith('/') && !next.startsWith('//') ? next : '/parent')

/** The PIN pad on its own, for screens outside the parent area that need a parent, like changing a kid's grade. */
function Unlock() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const gate = useParentGate()
  const next = safeNext(params.get('next'))
  const open = isOpen(gate.data)

  useEffect(() => {
    if (open) navigate(next, { replace: true })
  }, [open, next, navigate])

  if (gate.isPending || open) return <p style={{ padding: 24 }}>Checking…</p>
  return <UnlockScreen onUnlocked={() => navigate(next, { replace: true })} />
}

export function ParentUnlockPage() {
  return (
    <SignedInOnly>
      <Unlock />
    </SignedInOnly>
  )
}
