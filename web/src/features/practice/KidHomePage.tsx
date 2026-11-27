import { Link, useNavigate } from 'react-router'
import { courseLabel, examNames, jurisdictionLabel } from '../../shared/catalog/useCatalog'
import { learnerStore, type Learner } from '../../shared/learner/learner'
import { Avatar } from '../../shared/ui/Avatar'
import type { AvatarColor } from '../../shared/ui/avatarColors'
import { Button } from '../../shared/ui/Button'
import { Icon } from '../../shared/ui/Icon'
import { LearnerGuard } from './components/LearnerGuard'
import { sessionUrl } from './model/session'

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
              <div className="spacer">
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
              <Link to="/practice/tests" className="card" style={{ gridColumn: '1 / -1' }}>
                <Icon name="clock" size={36} color="var(--accent)" />
                <h2>{exams ? `Real ${exams} tests` : 'Released tests'}</h2>
                <p>Shuffle questions from every year, or take one year's full test. Answers at the end.</p>
              </Link>
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
