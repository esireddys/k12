import { useNavigate } from 'react-router'
import { courseLabel, courseLevelHint, hasQuestions, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { learnerStore, useLearner } from '../../shared/learner/learner'
import { TopBar } from '../../shared/ui/TopBar'
import { ParentLockedError, useSaveKidCourse } from './api/usePractice'

/** Pick a state and a grade or course (such as Algebra I). For a kid it is saved to their profile; for a guest it stays on this device. */
export function ChooseCoursePage() {
  const navigate = useNavigate()
  const learner = useLearner()
  const catalog = useCatalog()
  const save = useSaveKidCourse()
  const courses = catalog.data ?? []
  const byJurisdiction = [...new Map(courses.map((c) => [c.jurisdiction, jurisdictionLabel(c)])).entries()]

  const choose = (courseId: string) => {
    if (learner?.kind !== 'kid') {
      learnerStore.setCourse(courseId)
      navigate('/home')
      return
    }
    // A kid picks their first grade; changing it later is a parent's call, behind the parent PIN.
    save.mutate(
      { kidId: learner.kidId, courseId },
      {
        onSuccess: () => {
          learnerStore.setCourse(courseId)
          navigate('/home')
        },
        onError: (e) => {
          if (e instanceof ParentLockedError) navigate(`/parent/unlock?next=${encodeURIComponent('/course')}`)
        },
      },
    )
  }

  return (
    <main className="page">
      <TopBar back={learner?.kind === 'kid' ? '/kids' : '/'} title="Pick your grade or course" />
      {catalog.isError && <p role="alert">Could not load the courses. Is the API running?</p>}
      {save.isError && !(save.error instanceof ParentLockedError) && <p role="alert">{save.error.message}</p>}
      {byJurisdiction.map(([code, name]) => (
        <section key={code} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h2 style={{ margin: 0, fontSize: 22 }}>{name}</h2>
          <div className="grid-3">
            {courses
              .filter((c) => c.jurisdiction === code)
              .map((c) => (
                <button key={c.id} className="card" disabled={!hasQuestions(c)} onClick={() => choose(c.id)} style={{ opacity: hasQuestions(c) ? 1 : 0.6 }}>
                  <h3>{courseLabel(c)}</h3>
                  {courseLevelHint(c) && <p className="quiet">{courseLevelHint(c)}</p>}
                  <p>{hasQuestions(c) ? `${c.questionCount} questions` : 'Coming soon'}</p>
                </button>
              ))}
          </div>
        </section>
      ))}
    </main>
  )
}
