import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router'
import { useCurrentUser } from '../../shared/auth/useCurrentUser'
import { useProviderSignIn, useSignInOptions } from './api/useSignIn'
import { DeveloperSignIn } from './components/DeveloperSignIn'
import { GoogleButton } from './components/GoogleButton'
import { GuestStart } from './components/GuestStart'

/** The first screen: parents sign in with Google; anyone can try as a guest. */
export function WelcomePage() {
  const navigate = useNavigate()
  const me = useCurrentUser()
  const options = useSignInOptions()
  const google = options.data?.providers.find((p) => p.provider === 'google')
  const googleSignIn = useProviderSignIn('google')
  const toKids = () => navigate('/kids')

  // A parent who is already signed in goes straight to the kid picker.
  useEffect(() => {
    if (me.data?.hasFamily) navigate('/kids', { replace: true })
  }, [me.data?.hasFamily, navigate])

  return (
    <main className="page" style={{ minHeight: '100vh', justifyContent: 'center' }}>
      <div style={{ display: 'flex', gap: 40, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ flex: '1 1 380px', display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div className="brand">K12 Practice</div>
          <h1 style={{ margin: 0, fontSize: 46, fontWeight: 800, lineHeight: 1.15 }}>
            Practice real released test questions, a little every day.
          </h1>
          <p className="quiet" style={{ margin: 0, fontSize: 20, lineHeight: 1.5 }}>
            Pick your state and grade, then practice by topic, take a mix, or try a full test from a past year.
          </p>
          {me.data?.isOwner && <Link to="/owner/review">Content review (owners)</Link>}
        </div>

        <div style={{ flex: '1 1 380px', maxWidth: 440, display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="card">
            <h2 style={{ fontSize: 20 }}>Parents</h2>
            {google ? (
              <GoogleButton clientId={google.clientId} onToken={(token) => googleSignIn.mutate(token, { onSuccess: toKids })} />
            ) : (
              options.isSuccess && (
                <p>Google sign-in is not set up on this server yet. Add a Google client id to the API to turn it on.</p>
              )
            )}
            {options.isError && <p role="alert">Could not reach the server. Is the API running?</p>}
            {googleSignIn.isPending && <p role="status">Signing you in…</p>}
            {googleSignIn.error && <p role="alert">{googleSignIn.error.message}</p>}
            <p style={{ fontSize: 14 }}>One parent account. Add each kid as a profile, no kid email needed.</p>
            {options.data?.developerSignIn && <DeveloperSignIn onDone={toKids} />}
          </div>
          <GuestStart />
        </div>
      </div>
    </main>
  )
}
