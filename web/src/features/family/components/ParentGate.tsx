import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { Button } from '../../../shared/ui/Button'
import { Icon } from '../../../shared/ui/Icon'
import { TopBar } from '../../../shared/ui/TopBar'
import { isOpen, useParentGate, useUnlock } from '../api/useParentArea'
import { useSignOut } from '../api/useFamily'
import { PinPad } from './PinPad'

const time = (iso: string) => new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })

/** "Parents only": the PIN pad. Shown in place of a parent screen while the parent areas are closed. */
export function UnlockScreen({ onUnlocked }: { onUnlocked?: () => void }) {
  const gate = useParentGate()
  const unlock = useUnlock()
  const signOut = useSignOut()
  const navigate = useNavigate()
  const [tries, setTries] = useState(0)
  const result = unlock.data
  const lockedUntil = result?.result === 'LockedOut' ? result.lockedUntil : gate.data?.lockedUntil

  return (
    <main className="page" style={{ alignItems: 'center', textAlign: 'center' }}>
      <div style={{ alignSelf: 'stretch' }}>
        <TopBar back="/kids" />
      </div>
      <Icon name="lock" size={44} color="var(--accent)" />
      <h1 style={{ margin: 0, fontSize: 32, fontWeight: 800 }}>Parents only</h1>
      <p className="quiet" style={{ margin: 0, fontSize: 18 }}>
        Enter your 4-digit parent PIN
      </p>
      <PinPad
        resetKey={tries}
        disabled={unlock.isPending || !!lockedUntil}
        onComplete={(pin) =>
          unlock.mutate(pin, {
            onSuccess: (r) => {
              if (r.result === 'Unlocked') onUnlocked?.()
              else setTries((t) => t + 1)
            },
          })
        }
      />
      <div role="alert" style={{ minHeight: 24, fontWeight: 700, color: 'var(--miss-ink)' }}>
        {lockedUntil
          ? `Too many wrong tries. Try again after ${time(lockedUntil)}, or sign in again.`
          : result?.result === 'Wrong'
            ? `That's not the PIN. ${result.triesLeft} ${Number(result.triesLeft) === 1 ? 'try' : 'tries'} left.`
            : unlock.error?.message}
      </div>
      {/* Signing in again proves it is the parent, and opens the parent areas so the PIN can be changed. */}
      <Button variant="ghost" onClick={() => signOut.mutate(undefined, { onSuccess: () => navigate('/') })}>
        Forgot PIN? Sign in again with Google
      </Button>
    </main>
  )
}

/**
 * Wraps a parent screen. While the parent areas are closed it shows the PIN pad instead; the API refuses the same
 * requests, so this is for the screen, not the security.
 */
export function ParentGate({ children }: { children: ReactNode }) {
  const gate = useParentGate()
  if (gate.isPending) return <p style={{ padding: 24 }}>Checking…</p>
  if (gate.isError) return <p style={{ padding: 24 }}>Could not reach the server.</p>
  if (!isOpen(gate.data)) return <UnlockScreen />
  return <>{children}</>
}
