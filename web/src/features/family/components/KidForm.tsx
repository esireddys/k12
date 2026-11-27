import { useState } from 'react'
import { courseLabel, hasQuestions, jurisdictionLabel, useCatalog } from '../../../shared/catalog/useCatalog'
import { Avatar } from '../../../shared/ui/Avatar'
import { avatarColors, type AvatarColor } from '../../../shared/ui/avatarColors'
import { Button } from '../../../shared/ui/Button'
import type { KidBody } from '../api/useFamily'

/** Name, picture colour and course for a kid. No email, photo or location, by design. */
export function KidForm({
  initial,
  submitLabel,
  busy,
  error,
  onSubmit,
  onCancel,
}: {
  initial?: KidBody
  submitLabel: string
  busy: boolean
  error?: string
  onSubmit: (body: KidBody) => void
  onCancel?: () => void
}) {
  const catalog = useCatalog()
  const [name, setName] = useState(initial?.name ?? '')
  const [avatar, setAvatar] = useState<AvatarColor>(initial?.avatar ?? 'Orange')
  const [courseId, setCourseId] = useState(initial?.courseId ?? '')

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit({ name: name.trim(), avatar, courseId: courseId || null })
      }}
      style={{ display: 'flex', flexDirection: 'column', gap: 14 }}
    >
      <div className="row" style={{ alignItems: 'flex-end' }}>
        <Avatar name={name || '?'} color={avatar} size={64} />
        <div className="field" style={{ minWidth: 200 }}>
          <label htmlFor="kid-name">First name or nickname</label>
          <input id="kid-name" value={name} maxLength={30} onChange={(e) => setName(e.target.value)} required autoComplete="off" />
        </div>
        <div className="field" style={{ minWidth: 220 }}>
          <label htmlFor="kid-course">Grade or course</label>
          <select id="kid-course" value={courseId} onChange={(e) => setCourseId(e.target.value)}>
            <option value="">Choose later</option>
            {(catalog.data ?? []).map((c) => (
              <option key={c.id} value={c.id} disabled={!hasQuestions(c) && c.id !== initial?.courseId}>
                {courseLabel(c)} · {jurisdictionLabel(c)}
                {hasQuestions(c) ? '' : ' (soon)'}
              </option>
            ))}
          </select>
        </div>
      </div>
      <fieldset style={{ border: 'none', padding: 0, margin: 0, display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
        <legend className="quiet" style={{ fontSize: 14, fontWeight: 700, marginBottom: 6 }}>
          Picture colour
        </legend>
        {avatarColors.map((color) => (
          <label key={color} style={{ cursor: 'pointer', borderRadius: 30, padding: 3, border: `3px solid ${color === avatar ? 'var(--accent)' : 'transparent'}` }}>
            <input type="radio" name="avatar" value={color} checked={color === avatar} onChange={() => setAvatar(color)} className="visually-hidden" />
            <Avatar name={name || '?'} color={color} size={44} />
            <span className="visually-hidden">{color}</span>
          </label>
        ))}
      </fieldset>
      <div className="row">
        <Button type="submit" disabled={busy || !name.trim()}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        )}
        {error && <span role="alert" style={{ color: 'var(--miss-ink)', fontWeight: 700 }}>{error}</span>}
      </div>
    </form>
  )
}
