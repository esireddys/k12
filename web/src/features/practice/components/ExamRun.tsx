import { useEffect, useState } from 'react'
import type { CatalogCourse } from '../../../shared/catalog/useCatalog'
import { Button } from '../../../shared/ui/Button'
import { Icon } from '../../../shared/ui/Icon'
import { useScore, type ScoredResponse, type StudentQuestion } from '../api/usePractice'
import { QuestionView } from './QuestionView'
import { Results } from './Results'

/** Exam mode: answer in any order, flag to come back, and see nothing until Finish. */
export function ExamRun({
  title,
  course,
  questions,
  onAgain,
}: {
  title: string
  course: CatalogCourse
  questions: StudentQuestion[]
  onAgain: () => void
}) {
  const [index, setIndex] = useState(0)
  const [responses, setResponses] = useState<Record<string, string>>({})
  const [flagged, setFlagged] = useState<Set<string>>(() => new Set())
  const [confirming, setConfirming] = useState(false)
  const [scored, setScored] = useState<Record<string, ScoredResponse>>()
  const [startedAt] = useState(() => Date.now())
  const [minutes, setMinutes] = useState(0)
  const score = useScore()

  useEffect(() => {
    if (scored) return
    const timer = window.setInterval(() => setMinutes(Math.floor((Date.now() - startedAt) / 60_000)), 15_000)
    return () => window.clearInterval(timer)
  }, [startedAt, scored])

  if (scored)
    return (
      <Results title={title} course={course} questions={questions} scored={scored} responses={responses} minutes={minutes} onAgain={onAgain} />
    )

  const q = questions[index]
  const answered = questions.filter((x) => responses[x.id]).length
  const unanswered = questions.length - answered

  const finish = () =>
    score.mutate(
      questions.map((x) => ({ questionId: x.id, response: responses[x.id] ?? '' })),
      {
        onSuccess: (data) => {
          setMinutes(Math.max(1, Math.round((Date.now() - startedAt) / 60_000)))
          setScored(Object.fromEntries(data.items.map((i) => [i.questionId, i])))
        },
      },
    )

  const toggleFlag = () =>
    setFlagged((f) => {
      const next = new Set(f)
      if (next.has(q.id)) next.delete(q.id)
      else next.add(q.id)
      return next
    })

  return (
    <>
      <div className="topbar">
        <div>
          <div style={{ fontSize: 22, fontWeight: 800 }}>{title} · Practice test</div>
          <div className="quiet">{minutes} min used · no time limit</div>
        </div>
        <div className="spacer">
          <Button variant="dark" onClick={() => setConfirming(true)}>
            Finish test
          </Button>
        </div>
      </div>

      <nav aria-label="Questions" style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {questions.map((x, i) => {
          const done = !!responses[x.id]
          const flag = flagged.has(x.id)
          return (
            <button
              key={x.id}
              type="button"
              onClick={() => setIndex(i)}
              aria-current={i === index ? 'step' : undefined}
              aria-label={`Question ${i + 1}${done ? ', answered' : ''}${flag ? ', flagged' : ''}`}
              style={{
                width: 44,
                height: 44,
                borderRadius: 12,
                border: `${i === index ? 3 : 2}px solid ${flag ? 'var(--miss)' : i === index ? 'var(--ink)' : done ? 'var(--accent)' : 'var(--border)'}`,
                background: done ? 'var(--accent)' : 'var(--surface)',
                color: done ? '#fff' : 'var(--ink)',
                fontWeight: 800,
                cursor: 'pointer',
              }}
            >
              {i + 1}
            </button>
          )
        })}
      </nav>
      <div className="quiet" style={{ fontSize: 14 }}>
        Blue = answered · orange outline = flagged · white = not answered yet
      </div>

      <div className="row" style={{ justifyContent: 'space-between' }}>
        <strong style={{ fontSize: 18 }}>
          Question {index + 1} · {q.answerKind === 'Choice' ? 'Pick one' : 'Type your answer'}
        </strong>
        <span className="quiet">No right or wrong shown until you finish the test.</span>
      </div>

      <QuestionView key={q.id} question={q} response={responses[q.id] ?? ''} onResponse={(v) => setResponses((r) => ({ ...r, [q.id]: v }))} />

      <div className="row" style={{ justifyContent: 'space-between' }}>
        <Button variant="secondary" onClick={() => setIndex(index - 1)} disabled={index === 0}>
          Back
        </Button>
        <Button variant="secondary" onClick={toggleFlag} aria-pressed={flagged.has(q.id)}>
          <span className="row" style={{ gap: 8 }}>
            <Icon name="flag" size={18} /> {flagged.has(q.id) ? 'Unflag' : 'Flag for later'}
          </span>
        </Button>
        {index < questions.length - 1 ? (
          <Button size="lg" onClick={() => setIndex(index + 1)}>
            Next
          </Button>
        ) : (
          <Button size="lg" onClick={() => setConfirming(true)}>
            Finish
          </Button>
        )}
      </div>

      {confirming && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="finish-title"
          style={{ position: 'fixed', inset: 0, background: 'rgba(27,35,51,0.45)', display: 'grid', placeItems: 'center', padding: 16, zIndex: 10 }}
        >
          <div className="card" style={{ maxWidth: 480, width: '100%', gap: 14 }}>
            <h2 id="finish-title">Finish the test?</h2>
            <div className="row">
              <span className="chip" style={{ fontSize: 16 }}>{unanswered} unanswered</span>
              <span className="chip" style={{ fontSize: 16 }}>{flagged.size} flagged to check</span>
            </div>
            <p>After you finish you'll see your score, the answer key and explanations. You can't change answers after that.</p>
            {score.error && <p role="alert">{score.error.message}</p>}
            <div className="row">
              <Button variant="secondary" onClick={() => setConfirming(false)} autoFocus>
                Go back and check
              </Button>
              <Button onClick={finish} disabled={score.isPending}>
                Finish and see results
              </Button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
