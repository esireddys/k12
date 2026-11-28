import { useState } from 'react'
import { Button } from '../../../shared/ui/Button'
import { useDeveloperSignIn } from '../api/useSignIn'

/** Shown only when the API runs in Development. Signs in by email with no Google account. */
export function DeveloperSignIn({ onDone }: { onDone: () => void }) {
  const [email, setEmail] = useState('parent@localhost')
  const [name, setName] = useState('')
  const signIn = useDeveloperSignIn()

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        signIn.mutate({ email, name: name || undefined }, { onSuccess: onDone })
      }}
      style={{ display: 'flex', flexDirection: 'column', gap: 10, paddingTop: 8, borderTop: '2px dashed var(--border)' }}
    >
      <div className="eyebrow">Local development</div>
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <div className="field">
          <label htmlFor="dev-email">Email</label>
          <input id="dev-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </div>
        <div className="field">
          <label htmlFor="dev-name">Name (optional)</label>
          <input id="dev-name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
      </div>
      <Button type="submit" variant="secondary" disabled={signIn.isPending}>
        Developer sign-in
      </Button>
      <span className="quiet" style={{ fontSize: 14 }}>
        Sign in as owner@localhost to see the owner screens.
      </span>
      {signIn.error && <span role="alert">{signIn.error.message}</span>}
    </form>
  )
}
