import { Link, useNavigate, useSearchParams } from 'react-router'
import type { CatalogCourse } from '../../shared/catalog/useCatalog'
import type { Learner } from '../../shared/learner/learner'
import { usePracticeSet } from './api/usePractice'
import { useMistakesSet, useTodaySet } from './api/useProgress'
import { ExamRun } from './components/ExamRun'
import { LearnerGuard } from './components/LearnerGuard'
import { PracticeRun } from './components/PracticeRun'
import { newSeed, readSession } from './model/session'

function Session({ learner, course }: { learner: Learner; course: CatalogCourse }) {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const session = readSession(params, course.id)
  const mistakes = session?.request.source === 'Mistakes'
  const kidId = learner.kind === 'kid' ? learner.kidId : null
  // A kid's Today set is built from their own mistakes and progress; a guest's is a mix from the whole course.
  const today = session?.request.source === 'Today' && !!kidId
  const fromBank = usePracticeSet(session && !mistakes && !today ? session.request : null)
  const fromMistakes = useMistakesSet(mistakes ? kidId : null, params.toString())
  const fromToday = useTodaySet(today ? kidId : null, today ? { count: session.request.count, seed: session.request.seed } : null)
  const set = mistakes ? fromMistakes : today ? fromToday : fromBank

  if (!session) return <p>That practice link is incomplete. <Link to="/home">Back home</Link></p>
  if (mistakes && !kidId)
    return (
      <p>
        Mistakes are saved only for kids with a profile. <Link to="/home">Back home</Link>
      </p>
    )
  if (set.isPending) return <p>Getting your questions…</p>
  if (set.isError)
    return (
      <p role="alert">
        {set.error.message} <Link to="/home">Back home</Link>
      </p>
    )
  if (set.data.questions.length === 0)
    return (
      <p>
        {mistakes ? 'Nothing to fix right now. Every question you missed is fixed!' : 'No questions here yet.'}{' '}
        <Link to="/home">Back home</Link>
      </p>
    )

  const again = () => {
    const next = new URLSearchParams(params)
    next.set('seed', String(newSeed()))
    navigate(`/practice/session?${next}`)
  }
  // The full practice test is always taken like the real one: timed, answers at the end.
  const Run = session.mode === 'exam' || session.request.source === 'PracticeTest' ? ExamRun : PracticeRun
  // Keyed by the URL so "Try another set" starts fresh.
  return (
    <Run
      key={params.toString()}
      title={set.data.title}
      course={course}
      questions={set.data.questions}
      passages={set.data.passages ?? []}
      timeLimitMinutes={set.data.timeLimitMinutes == null ? undefined : Number(set.data.timeLimitMinutes)}
      learner={learner}
      source={session.request.source}
      storageKey={`k12.${Run === ExamRun ? 'exam' : 'practice'}:${learner.kind === 'kid' ? learner.kidId : 'guest'}:${params.toString()}`}
      onAgain={again}
    />
  )
}

/** One practice or exam session, built from the URL (mode, source, and what to draw from). */
export function SessionPage() {
  return (
    <LearnerGuard>
      {(learner, course) => (
        <main className="page" style={{ gap: 18 }}>
          <Session learner={learner} course={course} />
        </main>
      )}
    </LearnerGuard>
  )
}
