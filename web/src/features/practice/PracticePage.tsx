import { useSearchParams } from 'react-router'
import { TypedAnswer } from './components/TypedAnswer'

/** Placeholder until the question endpoints exist: pass ?q=<question id> from the API log. */
export function PracticePage() {
  const [params] = useSearchParams()
  const questionId = params.get('q')

  return (
    <main style={{ maxWidth: 720, margin: '0 auto', padding: '48px 16px', display: 'grid', gap: 24 }}>
      <h1 style={{ margin: 0 }}>Practice</h1>
      <p style={{ fontSize: 22 }}>A school has 6 classes. Each class has 24 students. How many students are in the school?</p>
      {questionId ? <TypedAnswer questionId={questionId} /> : <p>Add ?q=&lt;question id&gt; to the URL.</p>}
    </main>
  )
}
