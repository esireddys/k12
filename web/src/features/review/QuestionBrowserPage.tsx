import { Link, useSearchParams } from 'react-router'
import { courseLabel, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { useBrowseQuestions, type BrowseFilters } from './api/useQuestionBrowser'
import { OwnerNav } from './components/OwnerNav'
import { OwnerOnly } from './components/OwnerOnly'
import { sourceLine } from './model/content'

const field = { font: 'inherit', fontSize: 16, padding: 8, borderRadius: 10, border: '2px solid var(--border)' } as const

const flag = (v: string | null) => (v === 'true' ? true : v === 'false' ? false : undefined)

/** Find any Live question by course, year, skill, explanation or family report, and open it to fix. */
function Browser() {
  const [params, setParams] = useSearchParams()
  const catalog = useCatalog().data ?? []
  const filters: BrowseFilters = {
    courseId: params.get('courseId') ?? undefined,
    year: params.get('year') ? Number(params.get('year')) : undefined,
    skill: params.get('skill') ?? undefined,
    hasExplanation: flag(params.get('hasExplanation')),
    reported: flag(params.get('reported')),
  }
  const list = useBrowseQuestions(filters)
  const course = catalog.find((c) => c.id === filters.courseId)
  const years = [...new Set((course ? [course] : catalog).flatMap((c) => c.forms.map((f) => Number(f.year))))].sort((a, b) => b - a)
  const set = (name: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(name, value)
    else next.delete(name)
    if (name === 'courseId') next.delete('skill')
    setParams(next, { replace: true })
  }

  return (
    <>
      <div className="row" style={{ gap: 12, alignItems: 'flex-end' }}>
        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Course</strong>
          <select value={filters.courseId ?? ''} onChange={(e) => set('courseId', e.target.value)} style={field}>
            <option value="">All</option>
            {catalog.map((c) => (
              <option key={c.id} value={c.id}>
                {courseLabel(c)} · {jurisdictionLabel(c)}
              </option>
            ))}
          </select>
        </label>
        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Year</strong>
          <select value={filters.year ?? ''} onChange={(e) => set('year', e.target.value)} style={field}>
            <option value="">All</option>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </label>
        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Skill</strong>
          <select value={filters.skill ?? ''} onChange={(e) => set('skill', e.target.value)} style={field} disabled={!course}>
            <option value="">{course ? 'All' : 'Pick a course first'}</option>
            {course?.categories.flatMap((cat) =>
              cat.skills.map((s) => (
                <option key={s.code} value={s.code}>
                  {s.code} {s.description}
                </option>
              )),
            )}
          </select>
        </label>
        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Explanation</strong>
          <select value={params.get('hasExplanation') ?? ''} onChange={(e) => set('hasExplanation', e.target.value)} style={field}>
            <option value="">Any</option>
            <option value="false">Missing</option>
            <option value="true">Has one</option>
          </select>
        </label>
        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Reports</strong>
          <select value={params.get('reported') ?? ''} onChange={(e) => set('reported', e.target.value)} style={field}>
            <option value="">Any</option>
            <option value="true">Reported by families</option>
          </select>
        </label>
      </div>

      {list.isPending && <p>Loading…</p>}
      {list.isError && <p role="alert">{list.error.message}</p>}
      {list.data && (
        <>
          <p className="quiet" style={{ margin: 0 }}>
            {list.data.total} {Number(list.data.total) === 1 ? 'question' : 'questions'}
            {Number(list.data.total) > list.data.items.length ? `, showing the first ${list.data.items.length}` : ''}
          </p>
          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'grid', gap: 8 }}>
            {list.data.items.map((q) => (
              <li key={q.id}>
                <Link
                  to={`/owner/review/${q.id}`}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    gap: 12,
                    flexWrap: 'wrap',
                    padding: 12,
                    borderRadius: 12,
                    border: `2px solid ${Number(q.openReports) > 0 ? 'var(--danger-line)' : 'var(--border)'}`,
                    background: 'var(--surface)',
                    color: 'var(--ink)',
                    textDecoration: 'none',
                  }}
                >
                  <strong>
                    {q.source ? `${q.source.courseName} ${q.source.subject} · ` : ''}
                    {sourceLine(q.source, q.skillCodes)}
                  </strong>
                  <span className="quiet">
                    {Number(q.openReports) > 0 && (
                      <b style={{ color: 'var(--danger-ink)' }}>
                        {q.openReports} {Number(q.openReports) === 1 ? 'report' : 'reports'} ·{' '}
                      </b>
                    )}
                    {q.hasExplanation ? 'Has an explanation' : 'No explanation yet'}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  )
}

export function QuestionBrowserPage() {
  return (
    <OwnerOnly>
      <main style={{ maxWidth: 1040, margin: '0 auto', padding: '32px 16px', display: 'grid', gap: 18 }}>
        <OwnerNav />
        <h1 style={{ margin: 0 }}>All questions</h1>
        <Browser />
      </main>
    </OwnerOnly>
  )
}
