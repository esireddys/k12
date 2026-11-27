import { Link, useNavigate, useSearchParams } from 'react-router'
import type { CatalogCourse } from '../../shared/catalog/useCatalog'
import type { Learner } from '../../shared/learner/learner'
import { usePracticeSet } from './api/usePractice'
import { useMistakesSet } from './api/useProgress'
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
  const fromBank = usePracticeSet(session && !mistakes ? session.request : null)
  const fromMistakes = useMistakesSet(mistakes ? kidId : null, params.toString())
  const set = mistakes ? fromMistakes : fromBank

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
  const Run = session.mode === 'exam' ? ExamRun : PracticeRun
  // Keyed by the URL so "Try another set" starts fresh.
  return (
    <Run
      key={params.toString()}
      title={set.data.title}
      course={course}
      questions={set.data.questions}
      learner={learner}
      source={session.request.source}
      storageKey={`k12.exam:${learner.kind === 'kid' ? learner.kidId : 'guest'}:${params.toString()}`}
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
