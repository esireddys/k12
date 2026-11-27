import { Link, useNavigate } from 'react-router'
import { courseLabel, examNames, jurisdictionLabel } from '../../shared/catalog/useCatalog'
import { learnerStore, type Learner } from '../../shared/learner/learner'
import { Avatar } from '../../shared/ui/Avatar'
import type { AvatarColor } from '../../shared/ui/avatarColors'
import { Button } from '../../shared/ui/Button'
import { Icon } from '../../shared/ui/Icon'
import { useKidSummary } from './api/useProgress'
import { LearnerGuard } from './components/LearnerGuard'
import { sessionUrl } from './model/session'

/** Streak and stars for a kid. Guests have none: nothing they do is saved. */
function Badges({ kidId }: { kidId: string }) {
  const summary = useKidSummary(kidId).data
  if (!summary) return null
  return (
    <div className="row" style={{ gap: 10 }}>
      <span className="chip" style={{ background: '#FFF1DE', color: '#8A4500', fontSize: 17, padding: '10px 16px' }}>
        {summary.streak === 1 ? '1-day streak' : `${summary.streak}-day streak`}
      </span>
      <span className="chip" style={{ background: 'var(--accent-soft)', color: 'var(--accent-dark)', fontSize: 17, padding: '10px 16px' }}>
        {summary.stars} {Number(summary.stars) === 1 ? 'star' : 'stars'}
      </span>
    </div>
  )
}

/** Questions the kid missed come back here until they get them right. */
function FixMistakesCard({ kidId }: { kidId: string }) {
  const count = Number(useKidSummary(kidId).data?.mistakesToFix ?? 0)
  const body = (
    <>
      <Icon name="retry" size={36} color="var(--miss)" />
      <h2>
        Fix my mistakes{' '}
        {count > 0 && (
          <span className="chip" style={{ background: 'var(--miss-soft)', color: 'var(--miss-ink)', fontSize: 16 }}>
            {count}
          </span>
        )}
      </h2>
      <p>{count > 0 ? 'Questions you missed come back until you get them right.' : 'Nothing to fix right now. Missed questions will show up here.'}</p>
    </>
  )
  return count > 0 ? (
    <Link to={sessionUrl('practice', { source: 'Mistakes' })} className="card">
      {body}
    </Link>
  ) : (
    <div className="card" aria-disabled="true" style={{ opacity: 0.75 }}>
      {body}
    </div>
  )
}

/** A kid's (or guest's) home: practice by topic, a mixed set, or released tests. */
export function KidHomePage() {
  const navigate = useNavigate()
  return (
    <LearnerGuard>
      {(learner, course) => {
        const exams = examNames(course)
        const name = learner.kind === 'kid' ? learner.name : 'Guest'
        return (
          <main className="page">
            <div className="topbar">
              {learner.kind === 'kid' ? (
                <Avatar name={learner.name} color={learner.avatar as AvatarColor} />
              ) : (
                <Avatar name="G" color="Blue" />
              )}
              <div>
                <div style={{ fontSize: 26, fontWeight: 800 }}>Hi, {name}!</div>
                <div className="quiet">
                  {courseLabel(course)} · {jurisdictionLabel(course)}
                  {exams && ` ${exams}`} · <Link to="/course">Change</Link>
                </div>
              </div>
              <div className="spacer row">
                {learner.kind === 'kid' && <Badges kidId={learner.kidId} />}
                <SwitchButton learner={learner} onDone={(to) => navigate(to)} />
              </div>
            </div>

            {learner.kind === 'guest' && (
              <div className="banner" style={{ fontSize: 15 }}>
                <Icon name="info" /> Guest practice: nothing is saved. A parent can sign in with Google to add kid profiles.
              </div>
            )}

            <div className="grid-2">
              <Link to="/practice/topics" className="card">
                <Icon name="grid" size={36} color="var(--accent)" />
                <h2>Practice by topic</h2>
                <p>Pick a skill. See the answer and why right after each question.</p>
              </Link>
              <Link to={sessionUrl('practice', { source: 'Shuffle', count: 10 })} className="card">
                <Icon name="shuffle" size={36} color="var(--accent)" />
                <h2>Mixed practice</h2>
                <p>10 questions from everything in {courseLabel(course)}.</p>
              </Link>
              <Link to="/practice/tests" className="card" style={learner.kind === 'kid' ? undefined : { gridColumn: '1 / -1' }}>
                <Icon name="clock" size={36} color="var(--accent)" />
                <h2>{exams ? `Real ${exams} tests` : 'Released tests'}</h2>
                <p>Shuffle questions from every year, or take one year's full test. Answers at the end.</p>
              </Link>
              {learner.kind === 'kid' && <FixMistakesCard kidId={learner.kidId} />}
            </div>
          </main>
        )
      }}
    </LearnerGuard>
  )
}

function SwitchButton({ learner, onDone }: { learner: Learner; onDone: (to: string) => void }) {
  if (learner.kind === 'kid')
    return (
      <Button variant="secondary" onClick={() => onDone('/kids')}>
        Switch kid
      </Button>
    )
  return (
    <Button
      variant="secondary"
      onClick={() => {
        learnerStore.clear()
        onDone('/')
      }}
    >
      Leave guest mode
    </Button>
  )
}
