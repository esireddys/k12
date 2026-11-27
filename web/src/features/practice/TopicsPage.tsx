import { Link } from 'react-router'
import { TopBar } from '../../shared/ui/TopBar'
import { LearnerGuard } from './components/LearnerGuard'
import { sessionUrl } from './model/session'

/** Topics come from the course's reporting categories and skills in curriculum data. */
export function TopicsPage() {
  return (
    <LearnerGuard>
      {(_, course) => (
        <main className="page">
          <TopBar back="/home" title="What do you want to practice?" />
          <div className="grid-2">
            {course.categories.map((cat) => {
              const count = Number(cat.questionCount)
              return (
                <section key={cat.number} className="card" style={{ gap: 12 }}>
                  <div className="eyebrow">Category {cat.number}</div>
                  <h2 style={{ fontSize: 22 }}>{cat.name}</h2>
                  {count > 0 ? (
                    <Link to={sessionUrl('practice', { source: 'Topic', category: Number(cat.number), count: 10 })} className="list-row" style={{ background: 'var(--accent-soft)', color: 'var(--accent-dark)' }}>
                      <span>Mix of this category</span>
                      <span>{count} questions</span>
                    </Link>
                  ) : (
                    <p>No questions here yet.</p>
                  )}
                  {cat.skills
                    .filter((s) => Number(s.questionCount) > 0)
                    .map((s) => (
                      <Link key={s.code} to={sessionUrl('practice', { source: 'Topic', skill: s.code, count: 10 })} className="list-row">
                        <span>{s.description || s.code}</span>
                        <span className="quiet" style={{ whiteSpace: 'nowrap' }}>
                          {s.code} · {s.questionCount}
                        </span>
                      </Link>
                    ))}
                </section>
              )
            })}
          </div>
        </main>
      )}
    </LearnerGuard>
  )
}
