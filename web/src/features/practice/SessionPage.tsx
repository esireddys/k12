import { Link, useNavigate, useSearchParams } from 'react-router'
import type { CatalogCourse } from '../../shared/catalog/useCatalog'
import { usePracticeSet } from './api/usePractice'
import { ExamRun } from './components/ExamRun'
import { LearnerGuard } from './components/LearnerGuard'
import { PracticeRun } from './components/PracticeRun'
import { newSeed, readSession } from './model/session'

function Session({ course }: { course: CatalogCourse }) {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const session = readSession(params, course.id)
  const set = usePracticeSet(session?.request ?? null)

  if (!session) return <p>That practice link is incomplete. <Link to="/home">Back home</Link></p>
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
        No questions here yet. <Link to="/home">Back home</Link>
      </p>
    )

  const again = () => {
    const next = new URLSearchParams(params)
    next.set('seed', String(newSeed()))
    navigate(`/practice/session?${next}`)
  }
  const Run = session.mode === 'exam' ? ExamRun : PracticeRun
  // Keyed by the URL so "Try another set" starts fresh.
  return <Run key={params.toString()} title={set.data.title} course={course} questions={set.data.questions} onAgain={again} />
}

/** One practice or exam session, built from the URL (mode, source, and what to draw from). */
export function SessionPage() {
  return (
    <LearnerGuard>
      {(_, course) => (
        <main className="page" style={{ gap: 18 }}>
          <Session course={course} />
        </main>
      )}
    </LearnerGuard>
  )
}
