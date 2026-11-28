import { useState } from 'react'
import { Link } from 'react-router'
import { categoryKidName, type CatalogCourse } from '../../../shared/catalog/useCatalog'
import type { Learner } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'
import type { ScoredResponse, StudentQuestion } from '../api/usePractice'
import { QuestionPicture } from './QuestionView'
import { resultMessage, timeText } from '../model/results'
import { sessionUrl } from '../model/session'
import { sourceText } from '../model/source'

/** The kid's pick with its value ("B · 16") when the choices are plain text. */
const pickedText = (q: StudentQuestion, said: string) => {
  const text = q.choicesAreText ? q.choices.find((c) => c.label === said)?.text : undefined
  return text ? `${said} · ${text}` : said
}
const answerText = (r: ScoredResponse) => (r.correctChoiceText ? `${r.correctAnswer} · ${r.correctChoiceText}` : r.correctAnswer)

/** Score, a breakdown by topic, and the answer key with explanations. */
export function Results({
  title,
  course,
  questions,
  scored,
  retried = {},
  responses,
  minutes,
  learner,
  onAgain,
}: {
  title: string
  course: CatalogCourse
  questions: StudentQuestion[]
  /** The first try at each question: what the score counts. */
  scored: Record<string, ScoredResponse>
  /** Practice only: the second try after a first miss. */
  retried?: Record<string, ScoredResponse>
  responses: Record<string, string>
  minutes?: number
  learner: Learner
  onAgain: () => void
}) {
  const [filter, setFilter] = useState<'missed' | 'all'>('missed')
  const total = questions.length
  const correct = questions.filter((q) => scored[q.id]?.isCorrect).length
  // Answered means checked in practice (a miss clears the box for the second try) or filled in on a test.
  const answered = questions.filter((q) => scored[q.id]?.answered).length
  const missedAnswered = answered - correct
  const missed = questions.filter((q) => !scored[q.id]?.isCorrect)
  const shown = filter === 'missed' ? missed : questions
  const canFix = learner.kind === 'kid' && missedAnswered > 0

  const byCategory = course.categories
    .map((cat) => {
      const inCat = questions.filter((q) => Number(q.category) === cat.number)
      return { name: categoryKidName(cat), total: inCat.length, correct: inCat.filter((q) => scored[q.id]?.isCorrect).length }
    })
    .filter((c) => c.total > 0)

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div className="row" style={{ alignItems: 'stretch', gap: 18 }}>
        <div className="card" style={{ flex: '1 1 280px', alignItems: 'center', textAlign: 'center', justifyContent: 'center' }}>
          <div className="eyebrow">{title} · Finished</div>
          <div style={{ fontSize: 64, fontWeight: 800, lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>
            {correct} / {total}
          </div>
          <div className="quiet">right</div>
          <p style={{ fontSize: 20 }}>{resultMessage({ total, answered, correct })}</p>
          {minutes != null && <p>Time: {timeText(minutes)}</p>}
          <div className="row" style={{ justifyContent: 'center' }}>
            {canFix ? (
              <Link to={sessionUrl('practice', { source: 'Mistakes' })} className="button-link">
                Fix these {missedAnswered} now
              </Link>
            ) : null}
            <Button variant={canFix ? 'secondary' : 'primary'} onClick={onAgain}>
              Try another set
            </Button>
            <Link to="/home" style={{ fontWeight: 800, padding: '12px 8px' }}>
              Back home
            </Link>
          </div>
        </div>
        {byCategory.length > 0 && (
          <div className="card" style={{ flex: '1 1 280px' }}>
            <h2 style={{ fontSize: 20 }}>By topic</h2>
            {byCategory.map((c) => (
              <div key={c.name} style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 16 }}>
                <span>{c.name}</span>
                <strong>
                  {c.correct}/{c.total}
                </strong>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="row">
        <h2 style={{ margin: 0, fontSize: 22 }}>Answer key</h2>
        <div className="spacer row" role="group" aria-label="Show">
          <Button variant={filter === 'missed' ? 'primary' : 'secondary'} onClick={() => setFilter('missed')} aria-pressed={filter === 'missed'}>
            Missed ({missed.length})
          </Button>
          <Button variant={filter === 'all' ? 'primary' : 'secondary'} onClick={() => setFilter('all')} aria-pressed={filter === 'all'}>
            All ({total})
          </Button>
        </div>
      </div>
      {shown.length === 0 && <p className="quiet">Nothing missed. Every answer was right!</p>}
      {shown.map((q) => {
        const r = scored[q.id]
        const second = retried[q.id]
        const n = questions.indexOf(q) + 1
        const said = responses[q.id]
        return (
          <article key={q.id} className="card" style={{ borderColor: r?.isCorrect ? 'var(--border)' : '#FDBA8C', gap: 8 }}>
            <div className="row" style={{ justifyContent: 'space-between' }}>
              <strong>
                Q{n}
                {q.skillDescription ? ` · ${q.skillDescription}` : ''}
              </strong>
              <span className="chip" style={r?.isCorrect ? { background: 'var(--good-soft)', color: 'var(--good-ink)' } : { background: 'var(--miss-soft)', color: 'var(--miss-ink)' }}>
                {r?.isCorrect ? 'Correct' : second?.isCorrect ? 'Right on the second try' : r?.answered ? 'Missed' : 'Not answered'}
              </span>
            </div>
            {r && (
              <div className="row" style={{ fontSize: 17 }}>
                {said && (
                  <span>
                    You said: <strong>{pickedText(q, said)}</strong>
                  </span>
                )}
                <span>
                  Correct: <strong style={{ color: 'var(--good-ink)' }}>{answerText(r)}</strong>
                </span>
              </div>
            )}
            {r?.explanation && <p style={{ color: 'var(--ink)' }}>{r.explanation}</p>}
            <details>
              <summary style={{ cursor: 'pointer', fontWeight: 700, color: 'var(--accent)', padding: '6px 0' }}>Show the question</summary>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, paddingTop: 8 }}>
                {!q.textNeedsReview && <div style={{ fontSize: 18, whiteSpace: 'pre-line' }}>{q.stem}</div>}
                <QuestionPicture question={q} compact />
                {sourceText(q.source) && <span className="quiet">{sourceText(q.source)}</span>}
              </div>
            </details>
          </article>
        )
      })}
    </div>
  )
}
