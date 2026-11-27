import { useEffect, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useCatalog, type CatalogCourse } from '../../../shared/catalog/useCatalog'
import { useLearner, type Learner } from '../../../shared/learner/learner'

/**
 * Practice screens need someone practicing and a course. Without a learner the student goes to the welcome screen;
 * without a course, to the course picker.
 */
export function LearnerGuard({ children }: { children: (learner: Learner, course: CatalogCourse) => ReactNode }) {
  const learner = useLearner()
  const catalog = useCatalog()
  const navigate = useNavigate()
  const course = catalog.data?.find((c) => c.id === learner?.courseId)

  useEffect(() => {
    if (!learner) navigate('/', { replace: true })
    else if (catalog.isSuccess && !course) navigate('/course', { replace: true })
  }, [learner, catalog.isSuccess, course, navigate])

  if (catalog.isError) return <p style={{ padding: 24 }}>Could not load the courses. Is the API running?</p>
  if (!learner || !course) return <p style={{ padding: 24 }}>Loading…</p>
  return <>{children(learner, course)}</>
}
