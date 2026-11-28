import { useEffect, useRef, useState } from 'react'
import type { CatalogCourse } from '../../../shared/catalog/useCatalog'
import type { Learner } from '../../../shared/learner/learner'
import { Button } from '../../../shared/ui/Button'
import { Icon } from '../../../shared/ui/Icon'
import type { PassageView, ScoredResponse, StudentQuestion } from '../api/usePractice'
import { WithPassage } from './PassagePane'
import { useSessionScoring } from '../api/useProgress'
import { useStoredState } from '../model/storedState'
import { timeLeftText } from '../model/timeLimit'
import { clearResume, learnerKey, saveResume } from '../model/resume'
import { QuestionNavigator } from './QuestionNavigator'
import { QuestionView } from './QuestionView'
import { ReportProblem } from './ReportProblem'
import { Results } from './Results'

type ExamState = { index: number; responses: Record<string, string>; flagged: string[]; startedAt: number }

/**
 * Exam mode: answer in any order, flag to come back, and see nothing until Finish. The answers so far are kept on
 * this device, so a refresh or a swipe back does not lose the test. A kid's test is saved at Finish.
 * A timed test (the full practice test) counts down and finishes by itself when the time is up, like the real one.
 */
export function ExamRun({
  title,
  course,
  questions,
  passages = [],
  timeLimitMinutes,
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
  /** The time limit of the real test, when this one is timed. */
  timeLimitMinutes?: number
  learner: Learner
  source: string
  /** Where the answers so far are kept on this device; one per set. */
  storageKey: string
  onAgain: () => void
}) {
  const [state, update, forget] = useStoredState<ExamState>(storageKey, () => ({
    index: 0,
    responses: {},
    flagged: [],
    startedAt: Date.now(),
  }))
  const { index, responses, startedAt } = state
  const flagged = new Set(state.flagged)
  const setIndex = (i: number) => update((s) => ({ ...s, index: i }))
  const [confirming, setConfirming] = useState(false)
  const [scored, setScored] = useState<Record<string, ScoredResponse>>()
  const [now, setNow] = useState(() => Date.now())
  const minutes = Math.floor((now - startedAt) / 60_000)
  const msLeft = timeLimitMinutes ? startedAt + timeLimitMinutes * 60_000 - now : null
  const timeUp = msLeft !== null && msLeft <= 0
  const { score } = useSessionScoring({ learner, mode: 'exam', source, title })
  const [finishedMinutes, setFinishedMinutes] = useState<number>()

  useEffect(() => {
    if (scored) return
    const timer = window.setInterval(() => setNow(Date.now()), timeLimitMinutes ? 5_000 : 15_000)
    return () => window.clearInterval(timer)
  }, [scored, timeLimitMinutes])

  // "Pick up where you left off" on the home screen, while the test is open in this tab.
  const resumeUrl = `${window.location.pathname}${window.location.search}`
  const answeredCount = questions.filter((x) => responses[x.id]).length
  useEffect(() => {
    if (scored) return
    saveResume(learnerKey(learner), { url: resumeUrl, title, mode: 'exam', done: answeredCount, total: questions.length })
  }, [scored, learner, resumeUrl, title, answeredCount, questions.length])

  // When the time is up the test hands itself in, like the real one. Also after a refresh past the limit.
  const finishRef = useRef<() => void>(() => {})
  useEffect(() => {
    if (timeUp && !scored && !score.isPending && !score.isError) finishRef.current()
  }, [timeUp, scored, score.isPending, score.isError])

  if (scored)
    return (
      <Results
        title={title}
        course={course}
        questions={questions}
        scored={scored}
        responses={responses}
        minutes={finishedMinutes ?? minutes}
        learner={learner}
        onAgain={onAgain}
      />
    )

  const q = questions[Math.min(index, questions.length - 1)]
  const unanswered = questions.length - answeredCount

  const finish = () =>
    score.mutate(
      { responses: questions.map((x) => ({ questionId: x.id, response: responses[x.id] ?? '' })), finish: true, startedAt },
      {
        onSuccess: (data) => {
          setFinishedMinutes(Math.min(Math.floor((Date.now() - startedAt) / 60_000), timeLimitMinutes ?? Infinity))
          setScored(Object.fromEntries(data.items.map((i) => [i.questionId, i])))
          forget()
          clearResume(learnerKey(learner), resumeUrl)
        },
      },
    )

  finishRef.current = finish

  const toggleFlag = () =>
    update((s) => ({ ...s, flagged: s.flagged.includes(q.id) ? s.flagged.filter((id) => id !== q.id) : [...s.flagged, q.id] }))

  return (
    <>
      <div className="topbar">
        <div>
          <div style={{ fontSize: 22, fontWeight: 800 }}>{title === 'Full practice test' ? title : `${title} · Practice test`}</div>
          <div className="quiet" role={msLeft !== null ? 'timer' : undefined} aria-live="off">
            {msLeft !== null ? timeLeftText(Math.max(0, Math.ceil(msLeft / 60_000))) : `${minutes} min used · no time limit`}
          </div>
        </div>
        <div className="spacer">
          <Button variant="dark" onClick={() => setConfirming(true)}>
            Finish test
          </Button>
        </div>
      </div>

      <QuestionNavigator
        questions={questions}
        index={index}
        answered={(id) => !!responses[id]}
        flagged={(id) => flagged.has(id)}
        onPick={setIndex}
      />

      <div className="row" style={{ justifyContent: 'space-between' }}>
        <strong style={{ fontSize: 18 }}>
          {q.answerKind === 'Choice' ? 'Pick one' : 'Type your answer'}
          {flagged.has(q.id) && (
            <span className="chip flag-chip">
              <Icon name="flag-filled" size={16} /> Flagged
            </span>
          )}
        </strong>
        <span className="quiet">No right or wrong shown until you finish the test.</span>
      </div>

      <WithPassage key={q.passageId ?? 'none'} passage={passages.find((p) => p.id === q.passageId)}>
        <QuestionView key={q.id} question={q} response={responses[q.id] ?? ''} onResponse={(v) => update((s) => ({ ...s, responses: { ...s.responses, [q.id]: v } }))} />
        <ReportProblem key={`report-${q.id}`} questionId={q.id} learner={learner} />
      </WithPassage>

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
