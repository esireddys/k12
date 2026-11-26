import type { ItemContent } from '../model/content'

const field = {
  width: '100%',
  boxSizing: 'border-box',
  font: 'inherit',
  fontSize: 18,
  padding: 10,
  borderRadius: 10,
  border: '2px solid var(--border)',
} as const

type Props = {
  value: ItemContent
  original: ItemContent
  onChange: (next: ItemContent) => void
}

function Changed({ when }: { when: boolean }) {
  return when ? <span style={{ color: '#7a5200', fontWeight: 700 }}> · changed from the extracted text</span> : null
}

/** The text students will see. Prefilled with the AI reviewer's cleanup; check each field against the picture. */
export function ContentEditor({ value, original, onChange }: Props) {
  const set = (patch: Partial<ItemContent>) => onChange({ ...value, ...patch })

  return (
    <div style={{ display: 'grid', gap: 14 }}>
      <label style={{ display: 'grid', gap: 4 }}>
        <span>
          <strong>Question</strong>
          <Changed when={value.stem !== original.stem} />
        </span>
        <textarea rows={4} value={value.stem} onChange={(e) => set({ stem: e.target.value })} style={field} />
        {value.stem !== original.stem && (
          <small style={{ color: 'var(--ink-quiet)' }}>Extracted: {original.stem || '(empty)'}</small>
        )}
      </label>

      {value.choices.map((choice, i) => {
        const before = original.choices.find((c) => c.label === choice.label)?.text ?? ''
        return (
          <label key={choice.label} style={{ display: 'grid', gap: 4 }}>
            <span>
              <strong>Choice {choice.label}</strong>
              <Changed when={choice.text !== before} />
            </span>
            <input
              value={choice.text}
              onChange={(e) =>
                set({ choices: value.choices.map((c, j) => (j === i ? { ...c, text: e.target.value } : c)) })
              }
              style={field}
            />
            {choice.text !== before && <small style={{ color: 'var(--ink-quiet)' }}>Extracted: {before || '(empty)'}</small>}
          </label>
        )
      })}

      <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <input type="checkbox" checked={value.showImage} onChange={(e) => set({ showImage: e.target.checked })} />
        Show the picture to students (needed when the item has a figure, table or stacked fractions)
      </label>

      <label style={{ display: 'grid', gap: 4 }}>
        <strong>Figure description (read aloud, and shown if the picture fails)</strong>
        <textarea
          rows={2}
          value={value.figureDescription}
          onChange={(e) => set({ figureDescription: e.target.value })}
          style={field}
        />
      </label>

      <label style={{ display: 'grid', gap: 4 }}>
        <strong>Explanation (practice mode only)</strong>
        <textarea rows={4} value={value.explanation} onChange={(e) => set({ explanation: e.target.value })} style={field} />
      </label>
    </div>
  )
}
