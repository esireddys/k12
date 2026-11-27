import { useState } from 'react'
import { gradeLabel, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { Avatar } from '../../shared/ui/Avatar'
import { Button } from '../../shared/ui/Button'
import { TopBar } from '../../shared/ui/TopBar'
import { useAddKid, useFamily, useRemoveKid, useUpdateKid, type Kid } from './api/useFamily'
import { KidForm } from './components/KidForm'
import { useParentGate } from './api/useParentArea'
import { ParentGate } from './components/ParentGate'
import { PrivacyNote } from './components/PrivacyNote'
import { SetPinCard } from './components/SetPinCard'
import { SignedInOnly } from './components/SignedInOnly'

function KidRow({ kid, takenColors }: { kid: Kid; takenColors: Kid['avatar'][] }) {
  const course = useCatalog().data?.find((c) => c.id === kid.courseId)
  const update = useUpdateKid()
  const remove = useRemoveKid()
  const [editing, setEditing] = useState(false)
  const [confirming, setConfirming] = useState(false)

  if (editing)
    return (
      <div className="card">
        <KidForm
          initial={{ name: kid.name, avatar: kid.avatar, courseId: kid.courseId }}
          takenColors={takenColors}
          submitLabel="Save"
          busy={update.isPending}
          error={update.error?.message}
          onSubmit={(body) => update.mutate({ id: kid.id, ...body }, { onSuccess: () => setEditing(false) })}
          onCancel={() => setEditing(false)}
        />
      </div>
    )

  return (
    <div className="card" style={{ flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap' }}>
      <Avatar name={kid.name} color={kid.avatar} />
      <div style={{ flex: 1, minWidth: 160 }}>
        <div style={{ fontSize: 22, fontWeight: 800 }}>{kid.name}</div>
        <div className="quiet">{course ? `${gradeLabel(course)} · ${jurisdictionLabel(course)}` : 'No grade or course picked yet'}</div>
      </div>
      {confirming ? (
        <div className="row">
          <span style={{ fontWeight: 700 }}>Remove {kid.name}?</span>
          <Button variant="dark" onClick={() => remove.mutate(kid.id)} disabled={remove.isPending}>
            Remove
          </Button>
          <Button variant="secondary" onClick={() => setConfirming(false)}>
            Keep
          </Button>
        </div>
      ) : (
        <div className="row">
          <Button variant="secondary" onClick={() => setEditing(true)}>
            Edit
          </Button>
          <Button variant="ghost" onClick={() => setConfirming(true)}>
            Remove
          </Button>
        </div>
      )}
      {remove.error && <span role="alert">{remove.error.message}</span>}
    </div>
  )
}

/**
 * Right after the first kid is added, while there is no PIN: ask for one, so kids can't open the parent screens
 * on a shared tablet. "Skip for now" leaves it to the dashboard's reminder.
 */
function PinPrompt({ onSkip }: { onSkip: () => void }) {
  return (
    <section className="card" aria-labelledby="pin-prompt" style={{ gap: 12, borderColor: 'var(--accent)', borderWidth: 3 }}>
      <h2 id="pin-prompt" style={{ fontSize: 20 }}>
        One more step: set a parent PIN
      </h2>
      <p style={{ margin: 0 }}>Without a PIN, anyone using this device can open your dashboard and change your kids' settings.</p>
      <SetPinCard hasPin={false} onDone={onSkip} />
      <Button variant="ghost" style={{ alignSelf: 'center' }} onClick={onSkip}>
        Skip for now
      </Button>
    </section>
  )
}

function Manage() {
  const family = useFamily()
  const gate = useParentGate()
  const add = useAddKid()
  const [formKey, setFormKey] = useState(0)
  const [askPin, setAskPin] = useState(false)
  const kids = family.data?.kids ?? []

  return (
    <main className="page">
      <TopBar back="/parent" title="Your family" />
      <p className="quiet" style={{ margin: 0 }}>
        Kids practice under your sign-in. A profile keeps only a first name or nickname, a picture colour and a grade:
        no email, photo or location.
      </p>
      {kids.map((kid) => (
        <KidRow key={kid.id} kid={kid} takenColors={kids.filter((k) => k.id !== kid.id).map((k) => k.avatar)} />
      ))}
      {askPin && gate.data && !gate.data.hasPin && <PinPrompt onSkip={() => setAskPin(false)} />}
      {kids.length < 8 ? (
        <div className="card">
          <h2 style={{ fontSize: 20 }}>Add a kid</h2>
          <KidForm
            key={formKey}
            submitLabel="Add kid"
            busy={add.isPending}
            error={add.error?.message}
            takenColors={kids.map((k) => k.avatar)}
            onSubmit={(body) =>
              add.mutate(body, {
                onSuccess: () => {
                  setFormKey((k) => k + 1)
                  if (kids.length === 0) setAskPin(true)
                },
              })
            }
          />
        </div>
      ) : (
        <p className="quiet">A family can have up to 8 kids.</p>
      )}
      <PrivacyNote />
      <p className="quiet" style={{ fontSize: 14 }}>
        Signed in as {family.data?.displayName} ({family.data?.email}).
      </p>
    </main>
  )
}

export function ManageFamilyPage() {
  return (
    <SignedInOnly>
      <ParentGate>
        <Manage />
      </ParentGate>
    </SignedInOnly>
  )
}
