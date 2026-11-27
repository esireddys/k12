import { useEffect, useRef, useState } from 'react'

type CredentialResponse = { credential: string }
type GoogleId = {
  initialize(options: { client_id: string; callback: (r: CredentialResponse) => void; ux_mode?: 'popup' }): void
  renderButton(el: HTMLElement, options: Record<string, string | number>): void
}
declare global {
  interface Window {
    google?: { accounts: { id: GoogleId } }
  }
}

let loading: Promise<GoogleId> | undefined

/** Loads Google Identity Services once. */
function loadGoogle(): Promise<GoogleId> {
  loading ??= new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.onload = () => (window.google ? resolve(window.google.accounts.id) : reject(new Error('Google did not load')))
    script.onerror = () => {
      loading = undefined
      reject(new Error('Could not load Google sign-in'))
    }
    document.head.appendChild(script)
  })
  return loading
}

/**
 * Google's own "Continue with Google" button. Google hands back an ID token, which goes to the API;
 * the browser never keeps it. Needs this page's origin listed in the Google client's JavaScript origins.
 */
export function GoogleButton({ clientId, onToken }: { clientId: string; onToken: (token: string) => void }) {
  const host = useRef<HTMLDivElement>(null)
  const callback = useRef(onToken)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    callback.current = onToken
  }, [onToken])

  useEffect(() => {
    let cancelled = false
    loadGoogle()
      .then((google) => {
        if (cancelled || !host.current) return
        google.initialize({ client_id: clientId, callback: (r) => callback.current(r.credential), ux_mode: 'popup' })
        google.renderButton(host.current, {
          type: 'standard',
          theme: 'filled_blue',
          size: 'large',
          text: 'continue_with',
          shape: 'pill',
          width: 320,
        })
      })
      .catch(() => !cancelled && setFailed(true))
    return () => {
      cancelled = true
    }
  }, [clientId])

  if (failed) return <p role="alert">Google sign-in could not load. Check your connection and refresh.</p>
  return <div ref={host} style={{ minHeight: 44 }} />
}
