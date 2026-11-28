import { NavLink, Link } from 'react-router'
import { useQuestionReports } from '../api/useQuestionBrowser'

/** The owner area's header: back to the app, and the queue, the question browser and family reports. */
export function OwnerNav() {
  const reported = new Set((useQuestionReports().data ?? []).map((r) => r.questionId)).size
  return (
    <nav aria-label="Owner tools" className="row" style={{ gap: 16, alignItems: 'center' }}>
      <Link to="/kids">← Back to the app</Link>
      <NavLink to="/owner/review" end>
        Review queue
      </NavLink>
      <NavLink to="/owner/questions" end>
        All questions
      </NavLink>
      <Link to="/owner/questions?reported=true">Reported by families ({reported})</Link>
    </nav>
  )
}
