import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router'
import { useCurrentUser } from '../../shared/auth/useCurrentUser'
import { brand } from '../../shared/config/brand'
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
          <div className="brand">{brand.appName}</div>
          <h1 className="welcome-title">
            <span className="welcome-long">Practice real released test questions, a little every day.</span>
            <span className="welcome-short">Real test questions, a little every day.</span>
          </h1>
          {/* On a phone the sign-in and guest cards sit below the fold: two buttons jump straight to them. */}
          <div className="welcome-jump row">
            <a href="#parents" className="button-link">
              I'm a parent
            </a>
            <a href="#guest" className="button-link secondary">
              Just try it
            </a>
          </div>
          <p className="quiet welcome-lede">
            Pick your state and grade, then practice by topic, take a mix, or try a full test from a past year.
          </p>
          {me.data?.isOwner && <Link to="/owner/review">Content review (owners)</Link>}
        </div>

        <div style={{ flex: '1 1 380px', maxWidth: 440, display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div className="card" id="parents">
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
