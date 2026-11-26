import { useState } from 'react'
import { Button } from '../../../shared/ui/Button'
import { useCheckAnswer } from '../api/useCheckAnswer'

export function TypedAnswer({ questionId }: { questionId: string }) {
  const [value, setValue] = useState('')
  const check = useCheckAnswer(questionId)

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        check.mutate(value)
      }}
      style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}
    >
      <label htmlFor="answer">Your answer</label>
      <input
        id="answer"
        inputMode="decimal"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        style={{ fontSize: 24, padding: '10px 14px', borderRadius: 12, border: '2px solid var(--accent)' }}
      />
      <Button type="submit" disabled={check.isPending}>Check</Button>
      {check.data && <strong role="status">{check.data.isCorrect ? 'Correct!' : 'Not quite.'}</strong>}
      {check.error && <span role="alert">{check.error.message}</span>}
    </form>
  )
}
