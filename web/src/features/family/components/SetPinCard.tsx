import { useState } from 'react'
import { Button } from '../../../shared/ui/Button'
import { useSetPin } from '../api/useParentArea'
import { PinPad } from './PinPad'

/** Set or change the parent PIN: type it twice. */
export function SetPinCard({ hasPin, onDone }: { hasPin: boolean; onDone?: () => void }) {
  const save = useSetPin()
  const [first, setFirst] = useState<string | null>(null)
  const [mismatch, setMismatch] = useState(false)
  const [open, setOpen] = useState(!hasPin)

  if (!open)
    return (
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <span>Parent PIN</span>
        <Button variant="ghost" onClick={() => setOpen(true)}>
          Change
        </Button>
      </div>
    )

  if (save.isSuccess && !first)
    return (
      <p role="status" style={{ fontWeight: 700, color: 'var(--good-ink)' }}>
        PIN saved. Kids need it to open parent screens.
      </p>
    )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'center' }}>
      <strong style={{ fontSize: 18 }}>{first ? 'Type it again to check' : hasPin ? 'Pick a new 4-digit PIN' : 'Set a 4-digit parent PIN'}</strong>
      {!hasPin && !first && (
        <p className="quiet" style={{ margin: 0, textAlign: 'center' }}>
          The PIN keeps kids out of this dashboard and your family settings on a shared tablet.
        </p>
      )}
      <PinPad
        resetKey={`${first}-${mismatch}`}
        disabled={save.isPending}
        onComplete={(pin) => {
          if (!first) {
            setFirst(pin)
            setMismatch(false)
          } else if (pin !== first) {
            setFirst(null)
            setMismatch(true)
          } else {
            save.mutate(pin, {
              onSuccess: () => {
                setFirst(null)
                onDone?.()
              },
            })
          }
        }}
      />
      <div role="alert" style={{ minHeight: 24, fontWeight: 700, color: 'var(--miss-ink)' }}>
        {mismatch ? "Those didn't match. Start again." : save.error?.message}
      </div>
      {hasPin && (
        <Button variant="secondary" onClick={() => setOpen(false)}>
          Cancel
        </Button>
      )}
    </div>
  )
}
