import { useNavigate } from 'react-router'
import { courseLevelHint, gradeChoices, gradeLabel, hasQuestions, jurisdictionLabel, useCatalog, sameGradeSubjects, subjectLabel, type CatalogCourse } from '../../shared/catalog/useCatalog'
import { learnerStore, useLearner } from '../../shared/learner/learner'
import { TopBar } from '../../shared/ui/TopBar'
import { ParentLockedError, useSaveKidCourse } from './api/usePractice'

/** "Math and Reading" for a grade, so a kid knows both are inside; the question count for a single course. */
function subjectsText(c: CatalogCourse, courses: CatalogCourse[]) {
  if (c.kind === 'Course') return `${c.questionCount} questions`
  const subjects = sameGradeSubjects(c, courses).map((s) => subjectLabel(s.subject))
  return subjects.length > 1 ? `${subjects.slice(0, -1).join(', ')} and ${subjects[subjects.length - 1]}` : `${c.questionCount} questions`
}

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
      <TopBar back={learner?.kind === 'kid' ? '/kids' : '/'} title="Pick your grade" />
      {catalog.isError && <p role="alert">Could not load the courses. Is the API running?</p>}
      {save.isError && !(save.error instanceof ParentLockedError) && <p role="alert">{save.error.message}</p>}
      {byJurisdiction.map(([code, name]) => (
        <section key={code} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h2 style={{ margin: 0, fontSize: 22 }}>{name}</h2>
          <div className="grid-3">
            {gradeChoices(courses.filter((c) => c.jurisdiction === code), learner?.courseId).map((c) => (
              <button key={c.id} className="card" disabled={!hasQuestions(c)} onClick={() => choose(c.id)} style={{ opacity: hasQuestions(c) ? 1 : 0.6 }}>
                <h3>{gradeLabel(c)}</h3>
                {courseLevelHint(c) && <p className="quiet">{courseLevelHint(c)}</p>}
                <p>{hasQuestions(c) ? subjectsText(c, courses) : 'Coming soon'}</p>
              </button>
            ))}
          </div>
        </section>
      ))}
    </main>
  )
}
