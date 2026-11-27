import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { useOwnerQueue } from './api/useOwnerQueue'
import { useReviewDecision } from './api/useReviewDecision'
import { useReviewQuestion } from './api/useReviewQuestion'
import { ContentEditor } from './components/ContentEditor'
import { Findings } from './components/Findings'
import { ItemPicture } from './components/ItemPicture'
import { OwnerOnly } from './components/OwnerOnly'
import {
  difficulties,
  difficultyLabel,
  readContent,
  sourceLine,
  writeContent,
  type Difficulty,
  type QuestionForReview,
} from './model/content'

const input = {
  font: 'inherit',
  fontSize: 18,
  padding: 10,
  borderRadius: 10,
  border: '2px solid var(--border)',
} as const

/** The editable review form. Keyed by question id and version so it starts fresh for each one. */
function ReviewForm({ question, nextId }: { question: QuestionForReview; nextId?: string }) {
  const navigate = useNavigate()
  const decision = useReviewDecision(question.id)
  const original = readContent(question.content)
  const [content, setContent] = useState(() => readContent(question.aiReview?.proposedContent ?? question.content))
  const [answerValue, setAnswerValue] = useState(question.answer.value)
  const [difficulty, setDifficulty] = useState<Difficulty>(question.aiReview?.suggestedDifficulty ?? question.difficulty)
  const [note, setNote] = useState('')
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')

  const waiting = question.status === 'AwaitingOwner'
  const version = Number(question.version)
  const goNext = () => navigate(nextId ? `/owner/review/${nextId}` : '/owner/review')

  const approve = () =>
    decision.mutate(
      {
        kind: 'approve',
        version,
        content: writeContent(content),
        answer: { ...question.answer, value: answerValue.trim() },
        difficulty,
        note,
      },
      { onSuccess: goNext },
    )

  const reject = () => decision.mutate({ kind: 'reject', version, reason }, { onSuccess: goNext })

  return (
    <div style={{ display: 'grid', gap: 20, gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', alignItems: 'start' }}>
      <section style={{ display: 'grid', gap: 12, position: 'sticky', top: 12 }}>
        <ItemPicture path={original.image} />
        {question.source && (
          <a href={question.source.sourceUrl} target="_blank" rel="noreferrer">
            Open the released test
          </a>
        )}
      </section>

      <section style={{ display: 'grid', gap: 18 }}>
        <Findings review={question.aiReview} answer={question.answer.value} />

        {!waiting && (
          <p style={{ padding: 12, borderRadius: 12, background: 'var(--surface-muted)' }}>
            This question is {question.status}
            {question.ownerDecision &&
              `: ${question.ownerDecision.outcome} by ${question.ownerDecision.decidedBy}${
                question.ownerDecision.note ? ` (${question.ownerDecision.note})` : ''
              }`}
            .
          </p>
        )}

        <ContentEditor value={content} original={original} onChange={setContent} />

        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <label style={{ display: 'grid', gap: 4 }}>
            <strong>Answer ({question.answer.kind})</strong>
            <input value={answerValue} onChange={(e) => setAnswerValue(e.target.value)} style={{ ...input, width: 140 }} />
          </label>
          <label style={{ display: 'grid', gap: 4 }}>
            <strong>Difficulty</strong>
            <select value={difficulty} onChange={(e) => setDifficulty(e.target.value as Difficulty)} style={input}>
              {difficulties.map((d) => (
                <option key={d} value={d}>
                  {difficultyLabel[d]}
                  {d === question.aiReview?.suggestedDifficulty ? ' (AI suggests)' : ''}
                </option>
              ))}
            </select>
          </label>
        </div>

        {question.aiReview && !question.aiReview.answerKeyAgrees && answerValue.trim() === question.answer.value && (
          <p style={{ margin: 0, color: 'var(--danger-ink)', fontWeight: 700 }}>
            The AI reviewer disagrees with this key. Solve it yourself before you approve, or change the answer.
          </p>
        )}

        <label style={{ display: 'grid', gap: 4 }}>
          <strong>Note (optional)</strong>
          <input value={note} onChange={(e) => setNote(e.target.value)} style={input} />
        </label>

        {decision.isError && <p style={{ color: 'var(--danger-ink)' }}>{decision.error.message}</p>}

        {waiting && !rejecting && (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Button onClick={approve} disabled={decision.isPending || !content.stem.trim() || !answerValue.trim()}>
              Approve and go Live
            </Button>
            <Button variant="secondary" onClick={() => setRejecting(true)} disabled={decision.isPending}>
              Reject…
            </Button>
          </div>
        )}

        {waiting && rejecting && (
          <div style={{ display: 'grid', gap: 8 }}>
            <label style={{ display: 'grid', gap: 4 }}>
              <strong>Why reject it?</strong>
              <input autoFocus value={reason} onChange={(e) => setReason(e.target.value)} style={input} />
            </label>
            <div style={{ display: 'flex', gap: 12 }}>
              <Button onClick={reject} disabled={decision.isPending || !reason.trim()} style={{ background: 'var(--danger)' }}>
                Reject question
              </Button>
              <Button variant="secondary" onClick={() => setRejecting(false)}>
                Cancel
              </Button>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}

function Review({ questionId }: { questionId: string }) {
  const question = useReviewQuestion(questionId)
  const queue = useOwnerQueue()

  if (question.isPending) return <p>Loading…</p>
  if (question.isError) return <p>{question.error.message}</p>

  const items = queue.data?.items ?? []
  const index = items.findIndex((i) => i.id === questionId)
  const rest = index >= 0 ? [...items.slice(index + 1), ...items.slice(0, index)] : items
  const nextId = rest.find((i) => i.id !== questionId)?.id
  const q = question.data

  return (
    <>
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', flexWrap: 'wrap', gap: 8 }}>
        <h1 style={{ margin: 0, fontSize: 24 }}>{sourceLine(q.source, q.skills.map((s) => s.code))}</h1>
        <span>
          <Link to="/owner/review">Back to the queue</Link>
          {index >= 0 && ` · ${index + 1} of ${items.length}`}
        </span>
      </header>
      {q.skills.map((s) => (
        <p key={s.code} style={{ margin: 0, color: 'var(--ink-quiet)' }}>
          {s.code}: {s.description}
        </p>
      ))}
      <ReviewForm key={`${q.id}:${q.version}`} question={q} nextId={nextId} />
    </>
  )
}

/** One question: picture on the left, the AI reviewer's findings and the editable text on the right. */
export function ReviewQuestionPage() {
  const { questionId = '' } = useParams()
  return (
    <OwnerOnly>
      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '24px 16px', display: 'grid', gap: 16 }}>
        <Review questionId={questionId} />
      </main>
    </OwnerOnly>
  )
}
