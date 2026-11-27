import { useState } from 'react'
import { questionImageUrl, type StudentQuestion } from '../api/usePractice'
import { kindHint } from '../model/numberPad'
import { NumberPad } from './NumberPad'

export type Marking = { correct?: string; picked?: string; wasRight?: boolean }

/** The printed item. When the text came from a PDF and may be rough, the picture is what the student reads. */
export function QuestionPicture({ question, compact }: { question: StudentQuestion; compact?: boolean }) {
  const [broken, setBroken] = useState(false)
  if (!question.hasImage || !question.showImage || broken) return null
  return (
    <div style={{ borderRadius: 16, border: '2px solid var(--border)', background: '#fff', padding: compact ? 6 : 12, display: 'grid', placeItems: 'center' }}>
      <img
        src={questionImageUrl(question.id)}
        alt={question.figureDescription ?? 'The question as printed on the test'}
        onError={() => setBroken(true)}
        style={{ maxWidth: '100%', maxHeight: compact ? 280 : 520, height: 'auto' }}
      />
    </div>
  )
}

/** Stem, picture and the answer area: big choice buttons, or the answer box with the number pad. */
export function QuestionView({
  question,
  response,
  onResponse,
  locked,
  marking,
}: {
  question: StudentQuestion
  response: string
  onResponse: (value: string) => void
  locked?: boolean
  marking?: Marking
}) {
  const textOk = !question.textNeedsReview
  const choice = question.answerKind === 'Choice'

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className="card" style={{ gap: 16, padding: '20px 24px' }}>
        {textOk && question.stem && <div style={{ fontSize: 24, fontWeight: 700, lineHeight: 1.4 }}>{question.stem}</div>}
        <QuestionPicture question={question} />
        {!textOk && !question.hasImage && <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1.4 }}>{question.stem}</div>}
      </div>

      {choice ? (
        <div
          role="radiogroup"
          aria-label="Answer choices"
          style={{ display: 'grid', gridTemplateColumns: `repeat(${Math.min(question.choices.length, 4)}, minmax(0, 1fr))`, gap: 14 }}
        >
          {question.choices.map((c) => {
            const picked = response === c.label
            const isKey = marking?.correct === c.label
            const missed = marking && picked && !isKey
            const border = isKey ? 'var(--good)' : missed ? 'var(--miss)' : picked ? 'var(--accent)' : 'var(--border)'
            const background = isKey ? 'var(--good-soft)' : missed ? 'var(--miss-soft)' : picked ? 'var(--accent-soft)' : 'var(--surface)'
            return (
              <button
                key={c.label}
                type="button"
                role="radio"
                aria-checked={picked}
                disabled={locked}
                onClick={() => onResponse(c.label)}
                style={{
                  padding: '18px 16px',
                  minHeight: 72,
                  borderRadius: 16,
                  border: `3px solid ${border}`,
                  background,
                  fontSize: 24,
                  fontWeight: 800,
                  color: isKey ? 'var(--good-ink)' : missed ? 'var(--miss-ink)' : 'var(--ink)',
                  cursor: locked ? 'default' : 'pointer',
                }}
              >
                {c.label}
                {textOk && c.text ? ` · ${c.text}` : ''}
              </button>
            )
          })}
        </div>
      ) : (
        <div className="row" style={{ alignItems: 'flex-start', gap: 28 }}>
          <div style={{ flex: '1 1 260px', display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label htmlFor="typed-answer" style={{ fontSize: 18, fontWeight: 800 }}>
              Your answer
            </label>
            <output
              id="typed-answer"
              aria-live="polite"
              style={{
                minHeight: 64,
                padding: '12px 18px',
                borderRadius: 16,
                border: `3px solid ${marking ? (marking.wasRight ? 'var(--good)' : 'var(--miss)') : 'var(--accent)'}`,
                background: 'var(--surface)',
                fontSize: 32,
                fontWeight: 800,
                display: 'flex',
                alignItems: 'center',
                boxSizing: 'border-box',
              }}
            >
              {response || <span className="quiet" style={{ fontSize: 20, fontWeight: 600 }}>Tap the numbers</span>}
            </output>
            {kindHint[question.answerKind] && <span className="quiet">{kindHint[question.answerKind]}</span>}
          </div>
          <NumberPad kind={question.answerKind} value={response} onChange={onResponse} disabled={locked} />
        </div>
      )}
    </div>
  )
}
