import { useNavigate } from 'react-router'
import { courseLabel, hasQuestions, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { learnerStore, useLearner } from '../../shared/learner/learner'
import { TopBar } from '../../shared/ui/TopBar'
import { useSaveKidCourse } from './api/usePractice'

/** Pick a state and grade. For a kid it is saved to their profile; for a guest it stays on this device. */
export function ChooseCoursePage() {
  const navigate = useNavigate()
  const learner = useLearner()
  const catalog = useCatalog()
  const save = useSaveKidCourse()
  const courses = catalog.data ?? []
  const byJurisdiction = [...new Map(courses.map((c) => [c.jurisdiction, jurisdictionLabel(c)])).entries()]

  const choose = (courseId: string) => {
    learnerStore.setCourse(courseId)
    if (learner?.kind === 'kid') save.mutate({ kidId: learner.kidId, courseId })
    navigate('/home')
  }

  return (
    <main className="page">
      <TopBar back={learner?.kind === 'kid' ? '/kids' : '/'} title="Pick your grade" />
      {catalog.isError && <p role="alert">Could not load the courses. Is the API running?</p>}
      {byJurisdiction.map(([code, name]) => (
        <section key={code} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h2 style={{ margin: 0, fontSize: 22 }}>{name}</h2>
          <div className="grid-3">
            {courses
              .filter((c) => c.jurisdiction === code)
              .map((c) => (
                <button key={c.id} className="card" disabled={!hasQuestions(c)} onClick={() => choose(c.id)} style={{ opacity: hasQuestions(c) ? 1 : 0.6 }}>
                  <h3>{courseLabel(c)}</h3>
                  <p>{hasQuestions(c) ? `${c.questionCount} questions` : 'Coming soon'}</p>
                </button>
              ))}
          </div>
        </section>
      ))}
    </main>
  )
}
