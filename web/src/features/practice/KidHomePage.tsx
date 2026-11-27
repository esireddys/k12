import { Link, useNavigate } from 'react-router'
import {
  courseLabel,
  examNames,
  jurisdictionLabel,
  sameGradeSubjects,
  subjectLabel,
  useCatalog,
  type CatalogCourse,
} from '../../shared/catalog/useCatalog'
import { learnerStore, type Learner } from '../../shared/learner/learner'
import { Avatar } from '../../shared/ui/Avatar'
import type { AvatarColor } from '../../shared/ui/avatarColors'
import { Button } from '../../shared/ui/Button'
import { CategoryBars } from '../../shared/ui/CategoryBars'
import { Icon } from '../../shared/ui/Icon'
import { ParentLockedError, useSaveKidCourse } from './api/usePractice'
import { useKidSummary } from './api/useProgress'
import { LearnerGuard } from './components/LearnerGuard'
import { sessionUrl } from './model/session'
import { timeLimitText } from './model/timeLimit'

/**
 * Math, Reading, ...: the subjects of the kid's grade. Switching is the kid's own choice (no parent PIN): it is
 * saved to a kid's profile so their mistakes and progress follow, and kept on the device for a guest.
 */
function SubjectSwitcher({ learner, course }: { learner: Learner; course: CatalogCourse }) {
  const catalog = useCatalog()
  const save = useSaveKidCourse()
  const navigate = useNavigate()
  const subjects = sameGradeSubjects(course, catalog.data ?? [])
  if (subjects.length < 2) return null

  const choose = (courseId: string) => {
    if (courseId === course.id) return
    if (learner.kind !== 'kid') return learnerStore.setCourse(courseId)
    save.mutate(
      { kidId: learner.kidId, courseId },
      {
        onSuccess: () => learnerStore.setCourse(courseId),
        onError: (e) => {
          if (e instanceof ParentLockedError) navigate(`/parent/unlock?next=${encodeURIComponent('/home')}`)
        },
      },
    )
  }

  return (
    <div role="radiogroup" aria-label="Subject" className="row" style={{ gap: 10 }}>
      {subjects.map((s) => {
        const on = s.id === course.id
        return (
          <button
            key={s.id}
            type="button"
            role="radio"
            aria-checked={on}
            disabled={save.isPending}
            onClick={() => choose(s.id)}
            className="chip"
            style={{
              fontSize: 18,
              padding: '12px 20px',
              border: `3px solid ${on ? 'var(--accent)' : 'var(--border)'}`,
              background: on ? 'var(--accent)' : 'var(--surface)',
              color: on ? '#fff' : 'var(--ink)',
              fontWeight: 800,
              cursor: 'pointer',
            }}
          >
            {subjectLabel(s.subject)}
          </button>
        )
      })}
      {save.isError && !(save.error instanceof ParentLockedError) && <span role="alert">{save.error.message}</span>}
    </div>
  )
}

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

/** How the kid is doing in each part of the test. It fills as they practice anything in the course. */
function ProgressCard({ kidId }: { kidId: string }) {
  const categories = useKidSummary(kidId).data?.categories ?? []
  if (categories.length === 0) return null
  return (
    <section className="card" aria-labelledby="my-progress" style={{ gap: 14 }}>
      <h2 id="my-progress" style={{ fontSize: 20 }}>
        My progress
      </h2>
      <CategoryBars categories={categories} />
    </section>
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

            <SubjectSwitcher learner={learner} course={course} />

            {learner.kind === 'guest' && (
              <div className="banner" style={{ fontSize: 15 }}>
                <Icon name="info" /> Guest practice: nothing is saved. A parent can sign in with Google to add kid profiles.
              </div>
            )}

            {course.practiceTest && (
              <Link to={sessionUrl('exam', { source: 'PracticeTest' })} className="card" style={{ borderColor: 'var(--accent)', borderWidth: 3 }}>
                <Icon name="medal" size={36} color="var(--accent)" />
                <h2>Full practice test</h2>
                <p>
                  Just like the real {exams || 'test'}: {Number(course.practiceTest.itemCount)} questions
                  {course.practiceTest.minutes ? `, ${timeLimitText(Number(course.practiceTest.minutes))}` : ''}, a fresh mix from every year.
                  Answers at the end.
                </p>
              </Link>
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
                <p>{course.hasPassages ? 'A couple of reading selections' : '10 questions'} from everything in {courseLabel(course)}.</p>
              </Link>
              <Link to="/practice/tests" className="card" style={learner.kind === 'kid' ? undefined : { gridColumn: '1 / -1' }}>
                <Icon name="clock" size={36} color="var(--accent)" />
                <h2>{exams ? `Real ${exams} tests` : 'Released tests'}</h2>
                <p>Shuffle questions from every year, or take one year's full test. Answers at the end.</p>
              </Link>
              {learner.kind === 'kid' && <FixMistakesCard kidId={learner.kidId} />}
            </div>

            {learner.kind === 'kid' && <ProgressCard kidId={learner.kidId} />}
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
