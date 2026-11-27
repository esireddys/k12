import { useState } from 'react'
import { useNavigate } from 'react-router'
import { gradeChoices, gradeLabel, hasQuestions, jurisdictionLabel, useCatalog } from '../../../shared/catalog/useCatalog'
import { learnerStore } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'

/** Pick a state and a grade or course, then practice with nothing saved. */
export function GuestStart() {
  const navigate = useNavigate()
  const catalog = useCatalog()
  const courses = catalog.data ?? []
  const jurisdictions = [...new Map(courses.map((c) => [c.jurisdiction, jurisdictionLabel(c)])).entries()]

  const [picked, setPicked] = useState<string>()
  const jurisdiction = picked ?? courses.find(hasQuestions)?.jurisdiction ?? jurisdictions[0]?.[0] ?? ''
  // One choice per grade; Math, Reading and the other subjects are tabs on the home screen.
  const inJurisdiction = gradeChoices(courses.filter((c) => c.jurisdiction === jurisdiction))
  const [courseId, setCourseId] = useState<string>()
  const course = inJurisdiction.find((c) => c.id === courseId) ?? inJurisdiction.find(hasQuestions)

  const start = () => {
    if (!course) return
    learnerStore.set({ kind: 'guest', courseId: course.id })
    navigate('/home')
  }

  return (
    <div className="card" id="guest" style={{ borderStyle: 'dashed', borderColor: 'var(--border-strong)' }}>
      <h2 style={{ fontSize: 20 }}>Try as guest</h2>
      {catalog.isError && <p role="alert">Could not load the courses. Is the API running?</p>}
      <div className="row">
        <div className="field">
          <label htmlFor="g-state">State</label>
          <select
            id="g-state"
            value={jurisdiction}
            onChange={(e) => {
              setPicked(e.target.value)
              setCourseId(undefined)
            }}
          >
            {jurisdictions.map(([code, name]) => (
              <option key={code} value={code}>
                {name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="g-grade">Grade or course</label>
          <select id="g-grade" value={course?.id ?? ''} onChange={(e) => setCourseId(e.target.value)}>
            {inJurisdiction.map((c) => (
              <option key={c.id} value={c.id} disabled={!hasQuestions(c)}>
                {gradeLabel(c)}
                {hasQuestions(c) ? '' : ' (soon)'}
              </option>
            ))}
          </select>
        </div>
      </div>
      <Button variant="dark" onClick={start} disabled={!course || !hasQuestions(course)}>
        Start practicing
      </Button>
      <p className="quiet" style={{ margin: 0, fontSize: 15 }}>
        Guest practice isn't saved.
      </p>
    </div>
  )
}
