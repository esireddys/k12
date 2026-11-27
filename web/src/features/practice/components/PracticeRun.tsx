import { useState } from 'react'
import type { CatalogCourse } from '../../../shared/catalog/useCatalog'
import type { Learner } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'
import type { PassageView, ScoredResponse, StudentQuestion } from '../api/usePractice'
import { WithPassage } from './PassagePane'
import { useSessionScoring } from '../api/useProgress'
import { QuestionView } from './QuestionView'
import { Results } from './Results'
import { sourceText } from '../model/source'
import { SessionHeader } from './SessionHeader'

/** Practice mode: check each answer as you go and see the key and explanation right away. A kid's answers are saved as they go. */
export function PracticeRun({
  title,
  course,
  questions,
  passages = [],
  learner,
  source,
  onAgain,
}: {
  title: string
  course: CatalogCourse
  questions: StudentQuestion[]
  /** The reading selections the questions are about, if any. */
  passages?: PassageView[]
  /** Unused: practice is never timed. Accepted so both runs take the same props. */
  timeLimitMinutes?: number
  learner: Learner
  source: string
  /** Unused: practice keeps nothing on the device. Accepted so both runs take the same props. */
  storageKey?: string
  onAgain: () => void
}) {
  const [index, setIndex] = useState(0)
  const [responses, setResponses] = useState<Record<string, string>>({})
  const [scored, setScored] = useState<Record<string, ScoredResponse>>({})
  const [finished, setFinished] = useState(false)
  const { score, finish } = useSessionScoring({ learner, mode: 'practice', source, title })
  const seeResults = () => {
    setFinished(true)
    if (Object.keys(scored).length > 0) finish.mutate()
  }

  if (finished)
    return <Results title={title} course={course} questions={questions} scored={scored} responses={responses} onAgain={onAgain} />

  const q = questions[index]
  const response = responses[q.id] ?? ''
  const result = scored[q.id]
  const last = index === questions.length - 1

  const check = () =>
    score.mutate({ responses: [{ questionId: q.id, response }] }, {
      onSuccess: (data) => setScored((s) => ({ ...s, [q.id]: data.items[0] })),
    })

  return (
    <>
      <SessionHeader done={Object.keys(scored).length} total={questions.length} />
      <div className="row" style={{ gap: 8 }}>
        <span className="chip strong">{title}</span>
        {q.skillDescription && q.skillDescription !== title && <span className="chip">{q.skillDescription}</span>}
        {sourceText(q.source) && <span className="chip">{sourceText(q.source)}</span>}
      </div>

      <WithPassage passage={passages.find((p) => p.id === q.passageId)}>
      <QuestionView
        key={q.id}
        question={q}
        response={response}
        onResponse={(v) => setResponses((r) => ({ ...r, [q.id]: v }))}
        locked={!!result}
        marking={result ? { correct: result.correctAnswer, picked: response, wasRight: result.isCorrect } : undefined}
      />

      {result ? (
        <div
          role="status"
          className="card"
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            alignItems: 'center',
            background: result.isCorrect ? 'var(--good-soft)' : '#FFF7ED',
            borderColor: result.isCorrect ? '#86d19f' : '#FDBA8C',
          }}
        >
          <div style={{ flex: '1 1 300px', display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div style={{ fontSize: 22, fontWeight: 800, color: result.isCorrect ? 'var(--good-ink)' : 'var(--miss-ink)' }}>
              {result.isCorrect
                ? 'Correct!'
                : !result.understood
                  ? `That doesn't look like a ${q.answerKind.toLowerCase()} answer. The answer is ${result.correctAnswer}.`
                  : `Not quite. The answer is ${result.correctAnswer}${result.correctChoiceText ? `, ${result.correctChoiceText}` : ''}.`}
            </div>
            <div style={{ fontSize: 17, lineHeight: 1.5 }}>
              {result.explanation ?? (result.isCorrect ? 'Nice work.' : 'Look at the question again and see how the answer fits.')}
            </div>
          </div>
          <Button size="lg" onClick={() => (last ? seeResults() : setIndex(index + 1))} autoFocus>
            {last ? 'See results' : 'Next'}
          </Button>
        </div>
      ) : (
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          {score.error && <span role="alert">{score.error.message}</span>}
          <Button variant="secondary" onClick={() => (last ? seeResults() : setIndex(index + 1))}>
            Skip
          </Button>
          <Button size="lg" onClick={check} disabled={!response || score.isPending}>
            Check
          </Button>
        </div>
      )}
      </WithPassage>
    </>
  )
}
