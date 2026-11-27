import { Link, useNavigate } from 'react-router'
import { useCurrentUser } from '../../shared/auth/useCurrentUser'
import { courseLabel, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { learnerStore } from '../../shared/learner/learner'
import { Avatar } from '../../shared/ui/Avatar'
import { Button } from '../../shared/ui/Button'
import { Icon } from '../../shared/ui/Icon'
import { useFamily, useSignOut, type Kid } from './api/useFamily'
import { useLock } from './api/useParentArea'
import { SignedInOnly } from './components/SignedInOnly'

function KidCard({ kid, onPick }: { kid: Kid; onPick: () => void }) {
  const course = useCatalog().data?.find((c) => c.id === kid.courseId)
  return (
    <button className="card" onClick={onPick} style={{ width: 220, alignItems: 'center', padding: '28px 16px', borderWidth: 3 }}>
      <Avatar name={kid.name} color={kid.avatar} size={110} />
      <span style={{ fontSize: 26, fontWeight: 800 }}>{kid.name}</span>
      <span className="quiet" style={{ fontSize: 16 }}>
        {course ? `${courseLabel(course)} · ${jurisdictionLabel(course)}` : 'Pick a grade or course'}
      </span>
    </button>
  )
}

function Picker() {
  const navigate = useNavigate()
  const me = useCurrentUser()
  const family = useFamily()
  const signOut = useSignOut()
  const lock = useLock()
  const kids = family.data?.kids ?? []

  const pick = (kid: Kid) => {
    // Handing the tablet to a kid closes the parent areas, so they need the PIN again.
    lock.mutate()
    learnerStore.set({ kind: 'kid', kidId: kid.id, name: kid.name, avatar: kid.avatar, courseId: kid.courseId ?? undefined })
    navigate(kid.courseId ? '/home' : '/course')
  }

  return (
    <main className="page" style={{ minHeight: '100vh', gap: 40 }}>
      <div className="topbar">
        <span className="brand">K12 Practice</span>
        <div className="spacer row">
          {me.data?.isOwner && <Link to="/owner/review">Content review</Link>}
          <Link to="/parent" className="row" style={{ gap: 8, padding: '10px 16px', borderRadius: 12, border: '2px solid var(--border)', background: 'var(--surface)', textDecoration: 'none', color: 'var(--ink)', fontWeight: 700 }}>
            <Icon name="lock" size={18} /> Parent
          </Link>
          <Button variant="ghost" onClick={() => signOut.mutate(undefined, { onSuccess: () => navigate('/') })}>
            Sign out
          </Button>
        </div>
      </div>
      <div style={{ textAlign: 'center' }}>
        <h1 style={{ margin: 0, fontSize: 44, fontWeight: 800 }}>Who's practicing today?</h1>
        <p className="quiet" style={{ margin: '8px 0 0', fontSize: 20 }}>
          {kids.length > 0 ? 'Tap your picture to start' : 'Add your first kid to get started'}
        </p>
      </div>
      {family.isError && <p role="alert">{family.error.message}</p>}
      <div className="row" style={{ justifyContent: 'center', gap: 24, alignItems: 'stretch' }}>
        {kids.map((kid) => (
          <KidCard key={kid.id} kid={kid} onPick={() => pick(kid)} />
        ))}
        <Link
          to="/family"
          className="card"
          style={{ width: 220, alignItems: 'center', justifyContent: 'center', borderStyle: 'dashed', borderWidth: 3, borderColor: 'var(--border-strong)', background: 'transparent', color: 'var(--ink-quiet)' }}
        >
          <Icon name="plus" size={52} />
          <span style={{ fontSize: 20, fontWeight: 700 }}>Add a kid</span>
        </Link>
      </div>
      <p className="quiet" style={{ textAlign: 'center', marginTop: 'auto' }}>
        Signed in as {me.data?.name}.
      </p>
    </main>
  )
}

export function WhoIsPracticingPage() {
  return (
    <SignedInOnly>
      <Picker />
    </SignedInOnly>
  )
}
