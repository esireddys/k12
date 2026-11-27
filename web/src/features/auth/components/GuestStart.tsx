import { useState } from 'react'
import { useNavigate } from 'react-router'
import { courseLabel, hasQuestions, jurisdictionLabel, useCatalog } from '../../../shared/catalog/useCatalog'
import { learnerStore } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'

/** Pick a state and grade, then practice with nothing saved. */
export function GuestStart() {
  const navigate = useNavigate()
  const catalog = useCatalog()
  const courses = catalog.data ?? []
  const jurisdictions = [...new Map(courses.map((c) => [c.jurisdiction, jurisdictionLabel(c)])).entries()]

  const [picked, setPicked] = useState<string>()
  const jurisdiction = picked ?? courses.find(hasQuestions)?.jurisdiction ?? jurisdictions[0]?.[0] ?? ''
  const inJurisdiction = courses.filter((c) => c.jurisdiction === jurisdiction)
  const [courseId, setCourseId] = useState<string>()
  const course = inJurisdiction.find((c) => c.id === courseId) ?? inJurisdiction.find(hasQuestions)

  const start = () => {
    if (!course) return
    learnerStore.set({ kind: 'guest', courseId: course.id })
    navigate('/home')
  }

  return (
    <div className="card" style={{ borderStyle: 'dashed', borderColor: 'var(--border-strong)' }}>
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
          <label htmlFor="g-grade">Grade</label>
          <select id="g-grade" value={course?.id ?? ''} onChange={(e) => setCourseId(e.target.value)}>
            {inJurisdiction.map((c) => (
              <option key={c.id} value={c.id} disabled={!hasQuestions(c)}>
                {courseLabel(c)}
                {hasQuestions(c) ? '' : ' (soon)'}
              </option>
            ))}
          </select>
        </div>
      </div>
      <Button variant="dark" onClick={start} disabled={!course || !hasQuestions(course)}>
        Start practicing
      </Button>
      <div className="banner" style={{ fontSize: 14, padding: '10px 12px', borderWidth: 0, background: '#FFF7ED', color: '#9A3412' }}>
        Guest progress is not saved. It's gone when you leave.
      </div>
    </div>
  )
}
