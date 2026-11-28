import { Link, useNavigate } from 'react-router'
import {
  categoryKidName,
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
import { learnerKey, readResume } from './model/resume'
import { sessionUrl } from './model/session'

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
      <span className="chip badge-streak">
        <Icon name="sun" size={20} />
        {summary.streak === 1 ? '1-day streak' : `${summary.streak}-day streak`}
      </span>
      <span className="chip badge-reward">
        <Icon name="star-filled" size={20} />
        {summary.stars} {Number(summary.stars) === 1 ? 'star' : 'stars'}
      </span>
    </div>
  )
}

/** How the kid is doing in each part of the test, by the short kid names. It fills as they practice anything in the course. */
function ProgressCard({ kidId }: { kidId: string }) {
  const categories = useKidSummary(kidId).data?.categories ?? []
  if (categories.length === 0) return null
  return (
    <section className="card" aria-labelledby="my-progress" style={{ gap: 14 }}>
      <h2 id="my-progress" style={{ fontSize: 20 }}>
        My progress
      </h2>
      <CategoryBars categories={categories.map((c) => ({ ...c, name: categoryKidName(c) }))} />
    </section>
  )
}

/** Questions the kid missed come back here until they get them right. */
function FixMistakesCard({ count }: { count: number }) {
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

/** The set this learner left open in this tab, if any: a test or practice set they can go back to. */
function ResumeCard({ learner }: { learner: Learner }) {
  const point = readResume(learnerKey(learner))
  if (!point || point.done >= point.total) return null
  return (
    <Link to={point.url} className="card resume-card">
      <Icon name="retry" size={30} color="var(--accent)" />
      <div style={{ flex: 1 }}>
        <h2 style={{ fontSize: 20 }}>Pick up where you left off</h2>
        <p>
          {point.title}: {point.done} of {point.total} {point.mode === 'exam' ? 'answered' : 'done'}
        </p>
      </div>
    </Link>
  )
}

/**
 * A kid's (or guest's) home. One big way to start (Today's practice), Fix my mistakes right next to it when there
 * is something to fix, then practicing a topic and the tests.
 */
export function KidHomePage() {
  const navigate = useNavigate()
  return (
    <LearnerGuard>
      {(learner, course) => <Home learner={learner} course={course} onNavigate={(to) => navigate(to)} />}
    </LearnerGuard>
  )
}

function Home({ learner, course, onNavigate }: { learner: Learner; course: CatalogCourse; onNavigate: (to: string) => void }) {
  const exams = examNames(course)
  const name = learner.kind === 'kid' ? learner.name : 'Guest'
  const summary = useKidSummary(learner.kind === 'kid' ? learner.kidId : null).data
  const toFix = Number(summary?.mistakesToFix ?? 0)
  const isKid = learner.kind === 'kid'
  const practiceTest = course.practiceTest

  return (
    <main className="page">
      <div className="topbar">
        {isKid ? <Avatar name={learner.name} color={learner.avatar as AvatarColor} /> : <Avatar name="G" color="Blue" />}
        <div>
          <div style={{ fontSize: 26, fontWeight: 800 }}>Hi, {name}!</div>
          <div className="quiet">
            {courseLabel(course)} · {jurisdictionLabel(course)}
            {exams && ` ${exams}`} · <Link to="/course">Change</Link>
          </div>
        </div>
        <div className="spacer row">
          {isKid && <Badges kidId={learner.kidId} />}
          <SwitchButton learner={learner} onDone={onNavigate} />
        </div>
      </div>

      <SubjectSwitcher learner={learner} course={course} />

      {!isKid && (
        <div className="banner" style={{ fontSize: 15 }}>
          <Icon name="info" /> Guest practice isn't saved. A parent can sign in with Google to add kid profiles.
        </div>
      )}

      <ResumeCard learner={learner} />

      <div className="home-start">
        <Link to={sessionUrl('practice', { source: 'Today', count: 10 })} className="card today-card">
          <Icon name="sun" size={40} color="var(--reward)" />
          <h2>Today's practice</h2>
          <p>
            {isKid
              ? `About 10 questions: ${toFix > 0 ? 'a few you missed, then ' : ''}the part of ${subjectLabel(course.subject)} you're working on, and a little of everything.`
              : `About 10 questions from all of ${courseLabel(course)}.`}{' '}
            Check each answer as you go.
          </p>
        </Link>
        {isKid && toFix > 0 && <FixMistakesCard count={toFix} />}
      </div>

      <div className="grid-2">
        <Link to="/practice/topics" className="card">
          <Icon name="grid" size={36} color="var(--accent)" />
          <h2>Practice a topic</h2>
          <p>Pick one part of {subjectLabel(course.subject)}. See the answer right after each question.</p>
        </Link>
        <Link to="/practice/tests" className="card">
          <Icon name="clock" size={36} color="var(--accent)" />
          <h2>Tests</h2>
          <p>
            {practiceTest ? 'A full practice test, ' : ''}
            {exams ? `past ${exams} tests` : 'past released tests'} by year, or a shuffle of real questions. Answers at the end.
          </p>
        </Link>
        {isKid && toFix === 0 && <FixMistakesCard count={0} />}
      </div>

      {isKid && <ProgressCard kidId={learner.kidId} />}
    </main>
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
