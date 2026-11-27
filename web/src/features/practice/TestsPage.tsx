import { useState } from 'react'
import { Link } from 'react-router'
import { courseLabel, examNames } from '../../shared/catalog/useCatalog'
import { Icon } from '../../shared/ui/Icon'
import { TopBar } from '../../shared/ui/TopBar'
import { LearnerGuard } from './components/LearnerGuard'
import { sessionUrl } from './model/session'
import { timeLimitText } from './model/timeLimit'

/** Exam mode: Shuffle real questions from every year, or Pick a year's full test. No answers until Finish. */
export function TestsPage() {
  const [tab, setTab] = useState<'year' | 'shuffle'>('year')
  return (
    <LearnerGuard>
      {(_, course) => {
        const exams = examNames(course)
        const total = Number(course.questionCount)
        const full = Math.min(total, 60)
        const sizes = [...new Set([10, 20, full])].filter((n) => n <= total).sort((a, b) => a - b)
        return (
          <main className="page">
            <TopBar back="/home" title={exams ? `Real ${exams} tests` : 'Released tests'} />
            <div className="banner">
              <Icon name="info" /> No hints or answers during the test. The answer key and explanations unlock after you finish.
            </div>
            {course.practiceTest && (
              <Link to={sessionUrl('exam', { source: 'PracticeTest' })} className="card" style={{ borderColor: 'var(--accent)', borderWidth: 3 }}>
                <Icon name="medal" size={34} color="var(--accent)" />
                <h2>Full practice test</h2>
                <p>
                  {Number(course.practiceTest.itemCount)} real released questions in the same mix as the {exams || 'state'} test
                  {course.practiceTest.minutes ? `, with up to ${timeLimitText(Number(course.practiceTest.minutes))}` : ''}. A new mix every time.
                </p>
              </Link>
            )}

            <div className="grid-2">
              {(['shuffle', 'year'] as const).map((t) => (
                <button
                  key={t}
                  className="card"
                  aria-pressed={tab === t}
                  onClick={() => setTab(t)}
                  style={tab === t ? { borderColor: 'var(--accent)', borderWidth: 3, background: 'var(--accent-soft)' } : undefined}
                >
                  <Icon name={t === 'shuffle' ? 'shuffle' : 'calendar'} size={34} color="var(--accent)" />
                  <h2>{t === 'shuffle' ? 'Shuffle' : 'Pick a year'}</h2>
                  <p>
                    {t === 'shuffle'
                      ? 'A mix of real questions from every year we have. Pick 10, 20 or a full-length set.'
                      : 'One full released test in its original order. Answers and explanations at the end.'}
                  </p>
                </button>
              ))}
            </div>

            {tab === 'year' ? (
              <>
                <div className="eyebrow">Released tests · {courseLabel(course)}</div>
                {course.forms.length === 0 && <p className="quiet">No released tests yet for this grade.</p>}
                <div className="grid-3">
                  {course.forms.map((f) => (
                    <div key={f.id} className="card" style={{ gap: 6 }}>
                      <div style={{ fontSize: 28, fontWeight: 800 }}>{f.year}</div>
                      <div className="quiet">
                        {f.exam} · {f.questionCount} questions
                      </div>
                      <Link
                        to={sessionUrl('exam', { source: 'Year', formId: f.id })}
                        style={{ marginTop: 6, padding: 12, borderRadius: 12, background: 'var(--accent)', color: '#fff', textDecoration: 'none', textAlign: 'center', fontWeight: 800 }}
                      >
                        Start
                      </Link>
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <div className="grid-3">
                {sizes.map((n) => (
                  <Link key={n} to={sessionUrl('exam', { source: 'Shuffle', count: n })} className="card" style={{ alignItems: 'center' }}>
                    <div style={{ fontSize: 34, fontWeight: 800 }}>{n}</div>
                    <p>{n === full && n > 20 ? 'Full length' : 'questions'}</p>
                  </Link>
                ))}
              </div>
            )}
          </main>
        )
      }}
    </LearnerGuard>
  )
}
