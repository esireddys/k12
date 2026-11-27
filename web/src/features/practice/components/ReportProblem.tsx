import { useState } from 'react'
import type { Learner } from '../../../shared/learner/learner'
import { useReportQuestion, type ReportReason } from '../api/usePractice'

const reasons: { reason: ReportReason; label: string }[] = [
  { reason: 'WrongAnswer', label: 'The answer looks wrong' },
  { reason: 'HardToRead', label: 'Hard to read' },
  { reason: 'Confusing', label: "Doesn't make sense" },
  { reason: 'Typo', label: 'A typo' },
  { reason: 'Other', label: 'Something else' },
]

/**
 * "Report a problem" under a question: pick one reason from a short list. No typing on purpose, so a kid can't
 * send anything personal. Reports land in the owners' question browser.
 */
export function ReportProblem({ questionId, learner }: { questionId: string; learner: Learner }) {
  const [open, setOpen] = useState(false)
  const report = useReportQuestion()

  if (report.isSuccess)
    return (
      <p role="status" className="quiet report-problem" style={{ margin: 0 }}>
        Thanks for telling us. We'll check this question.
      </p>
    )

  return (
    <div className="report-problem">
      <button type="button" className="link-button" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        Report a problem with this question
      </button>
      {open && (
        <div className="row" role="group" aria-label="What's wrong?" style={{ gap: 8, marginTop: 8 }}>
          {reasons.map((r) => (
            <button
              key={r.reason}
              type="button"
              className="chip report-reason"
              disabled={report.isPending}
              onClick={() => report.mutate({ questionId, reason: r.reason, reporter: learner.kind === 'kid' ? 'Kid' : 'Guest' })}
            >
              {r.label}
            </button>
          ))}
          {report.isError && <span role="alert">{report.error.message}</span>}
        </div>
      )}
    </div>
  )
}
