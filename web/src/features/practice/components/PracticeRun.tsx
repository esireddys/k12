import { useEffect } from 'react'
import type { CatalogCourse } from '../../../shared/catalog/useCatalog'
import type { Learner } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'
import { Icon } from '../../../shared/ui/Icon'
import type { AnswerKind, PassageView, ScoredResponse, StudentQuestion } from '../api/usePractice'
import { useSessionScoring } from '../api/useProgress'
import { clearResume, learnerKey, saveResume } from '../model/resume'
import { sourceText } from '../model/source'
import { useStoredState } from '../model/storedState'
import { WithPassage } from './PassagePane'
import { QuestionView } from './QuestionView'
import { ReportProblem } from './ReportProblem'
import { Results } from './Results'
import { SessionHeader } from './SessionHeader'

/** How to type each kind of answer, said as a next step rather than as a mistake. */
const howToType: Record<Exclude<AnswerKind, 'Choice'>, string> = {
  Integer: 'Type a whole number, like 24.',
  Decimal: 'Type a number, like 2.5 or -3.',
  Fraction: 'Type a fraction, like 3/4.',
  Money: 'Type dollars and cents, like $2.75.',
  Time: 'Type hours and minutes, like 3:45.',
}

type PracticeState = {
  sessionId: string
  index: number
  responses: Record<string, string>
  /** The first check of each question: the one that counts for the score and for progress. */
  scored: Record<string, ScoredResponse>
  /** The second check, after "Try once more". */
  retried: Record<string, ScoredResponse>
  /** Choices already tried on a question, struck out for the second go. */
  struck: Record<string, string[]>
  finished: boolean
}

/**
 * Practice mode: check each answer as you go. A first miss says "try once more" and strikes out that choice;
 * the answer and why come after the second try. Only the first try counts. A kid's answers are saved as they go,
 * and the set is kept on this device, so a refresh picks up on the same question.
 */
export function PracticeRun({
  title,
  course,
  questions,
  passages = [],
  learner,
  source,
  storageKey,
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
  /** Where the set so far is kept on this device; one per set. */
  storageKey: string
  onAgain: () => void
}) {
  const [state, update] = useStoredState<PracticeState>(storageKey, () => ({
    sessionId: crypto.randomUUID(),
    index: 0,
    responses: {},
    scored: {},
    retried: {},
    struck: {},
    finished: false,
  }))
  const { index, responses, scored, retried, struck, finished } = state
  const { score, finish } = useSessionScoring({ learner, mode: 'practice', source, title, sessionId: state.sessionId })
  const resumeUrl = `${window.location.pathname}${window.location.search}`
  const done = Object.keys(scored).length

  useEffect(() => {
    if (finished) clearResume(learnerKey(learner), resumeUrl)
    else saveResume(learnerKey(learner), { url: resumeUrl, title, mode: 'practice', done, total: questions.length })
  }, [finished, learner, resumeUrl, title, done, questions.length])

  const seeResults = () => {
    // Kept as finished, so a refresh on the results shows them again rather than restarting the set.
    update((s) => ({ ...s, finished: true }))
    if (done > 0) finish.mutate()
  }

  if (finished)
    return (
      <Results
        title={title}
        course={course}
        questions={questions}
        scored={scored}
        retried={retried}
        responses={responses}
        learner={learner}
        onAgain={onAgain}
      />
    )

  const q = questions[Math.min(index, questions.length - 1)]
  const response = responses[q.id] ?? ''
  const first = scored[q.id]
  const second = retried[q.id]
  // A first miss on something the checker understood gets one more try; the answer waits until then.
  const tryingAgain = !!first && !first.isCorrect && !second
  const result = second ?? (tryingAgain ? undefined : first)
  const last = index === questions.length - 1
  const next = () => (last ? seeResults() : update((s) => ({ ...s, index: s.index + 1 })))

  const check = () =>
    score.mutate(
      { responses: [{ questionId: q.id, response }] },
      {
        onSuccess: (data) => {
          const item = data.items[0]
          update((s) => {
            if (!s.scored[q.id]) {
              const missed = !item.isCorrect
              return {
                ...s,
                scored: { ...s.scored, [q.id]: item },
                // Clear the miss so the kid picks or types again, and strike out a choice they already tried.
                responses: missed ? { ...s.responses, [q.id]: '' } : s.responses,
                struck: missed && q.answerKind === 'Choice' ? { ...s.struck, [q.id]: [response] } : s.struck,
              }
            }
            return { ...s, retried: { ...s.retried, [q.id]: item } }
          })
        },
      },
    )

  const typedHint = q.answerKind === 'Choice' ? '' : howToType[q.answerKind]
  const missText = (r: ScoredResponse) =>
    `The answer is ${r.correctAnswer}${r.correctChoiceText ? `, ${r.correctChoiceText}` : ''}${q.unit ? ` ${q.unit}` : ''}.`

  return (
    <>
      <SessionHeader done={done} total={questions.length} />
      <div className="row" style={{ gap: 8 }}>
        <span className="chip strong">{title}</span>
        {q.skillDescription && q.skillDescription !== title && <span className="chip">{q.skillDescription}</span>}
        {sourceText(q.source) && <span className="chip">{sourceText(q.source)}</span>}
      </div>

      <WithPassage key={q.passageId ?? q.id} passage={passages.find((p) => p.id === q.passageId)}>
        <QuestionView
          key={q.id}
          question={q}
          response={response}
          onResponse={(v) => update((s) => ({ ...s, responses: { ...s.responses, [q.id]: v } }))}
          locked={!!result}
          marking={
            result
              ? { correct: result.correctAnswer, picked: response || undefined, wasRight: result.isCorrect }
              : tryingAgain
                ? { struck: struck[q.id] ?? [] }
                : undefined
          }
        />

        {tryingAgain && (
          <div role="status" className="card feedback feedback-retry">
            <div className="feedback-title">Not quite. Try once more.</div>
            <div>
              {!first.understood && typedHint
                ? typedHint
                : q.answerKind === 'Choice'
                  ? 'The one you picked is crossed out. Look at the question again and pick another.'
                  : 'Look at the question again and type a new answer.'}
            </div>
          </div>
        )}

        {result ? (
          <div role="status" className={`card feedback ${result.isCorrect ? 'feedback-good' : 'feedback-miss'}`}>
            <div style={{ flex: '1 1 300px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div className="feedback-title">
                {result.isCorrect ? (
                  <>
                    <Icon name="check" size={24} /> {second ? 'You got it on the second try!' : 'Correct!'}
                  </>
                ) : (
                  `Not this time. ${missText(result)}`
                )}
              </div>
              <div style={{ fontSize: 17, lineHeight: 1.5 }}>
                {result.explanation ?? (result.isCorrect ? 'Nice work.' : 'It will come back in Fix my mistakes, so you can try it again later.')}
              </div>
            </div>
            <Button size="lg" onClick={next} autoFocus>
              {last ? 'See results' : 'Next'}
            </Button>
          </div>
        ) : (
          <div className="row" style={{ justifyContent: 'flex-end' }}>
            {score.error && <span role="alert">{score.error.message}</span>}
            <Button variant="secondary" onClick={next}>
              Skip
            </Button>
            <Button size="lg" onClick={check} disabled={!response || score.isPending}>
              Check
            </Button>
          </div>
        )}
        <ReportProblem key={`report-${q.id}`} questionId={q.id} learner={learner} />
      </WithPassage>
    </>
  )
}
