import { useState } from 'react'
import { Icon } from '../../../shared/ui/Icon'
import type { StudentQuestion } from '../api/usePractice'

/**
 * "Question 6 of 32": one line that opens the grid of every question, so the grid never pushes the question down.
 * Each button says its state with an icon as well as colour (a check when answered, a flag when flagged), and
 * the current question keeps its own marker whether or not it is flagged.
 */
export function QuestionNavigator({
  questions,
  index,
  answered,
  flagged,
  onPick,
}: {
  questions: StudentQuestion[]
  index: number
  answered: (id: string) => boolean
  flagged: (id: string) => boolean
  onPick: (index: number) => void
}) {
  const [open, setOpen] = useState(false)
  const done = questions.filter((q) => answered(q.id)).length
  const flags = questions.filter((q) => flagged(q.id)).length

  return (
    <div className="navigator">
      <button type="button" className="navigator-toggle" aria-expanded={open} aria-controls="question-grid" onClick={() => setOpen((o) => !o)}>
        <span>
          Question {index + 1} of {questions.length}
        </span>
        <span className="quiet navigator-counts">
          {done} answered{flags > 0 ? ` · ${flags} flagged` : ''}
        </span>
        <span aria-hidden="true" className={`navigator-chevron${open ? ' open' : ''}`}>
          ▾
        </span>
      </button>

      {open && (
        <div id="question-grid">
          <nav aria-label="Questions" className="navigator-grid">
            {questions.map((x, i) => {
              const isDone = answered(x.id)
              const flag = flagged(x.id)
              const current = i === index
              return (
                <button
                  key={x.id}
                  type="button"
                  onClick={() => {
                    onPick(i)
                    setOpen(false)
                  }}
                  aria-current={current ? 'step' : undefined}
                  aria-label={`Question ${i + 1}${isDone ? ', answered' : ''}${flag ? ', flagged' : ''}${current ? ', this question' : ''}`}
                  className={`navigator-item${isDone ? ' done' : ''}${current ? ' current' : ''}`}
                >
                  <span>{i + 1}</span>
                  {flag ? <Icon name="flag-filled" size={14} /> : isDone ? <Icon name="check" size={14} /> : null}
                </button>
              )
            })}
          </nav>
          <p className="quiet navigator-legend">
            <Icon name="check" size={14} /> answered · <Icon name="flag-filled" size={14} /> flagged to check · underline: the question you are on
          </p>
        </div>
      )}
    </div>
  )
}
