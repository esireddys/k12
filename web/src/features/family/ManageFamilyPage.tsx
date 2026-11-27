import { useState } from 'react'
import { courseLabel, jurisdictionLabel, useCatalog } from '../../shared/catalog/useCatalog'
import { Avatar } from '../../shared/ui/Avatar'
import { Button } from '../../shared/ui/Button'
import { TopBar } from '../../shared/ui/TopBar'
import { useAddKid, useFamily, useRemoveKid, useUpdateKid, type Kid } from './api/useFamily'
import { KidForm } from './components/KidForm'
import { SignedInOnly } from './components/SignedInOnly'

function KidRow({ kid }: { kid: Kid }) {
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
        <div className="quiet">{course ? `${courseLabel(course)} · ${jurisdictionLabel(course)}` : 'No grade picked yet'}</div>
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

function Manage() {
  const family = useFamily()
  const add = useAddKid()
  const [formKey, setFormKey] = useState(0)
  const kids = family.data?.kids ?? []

  return (
    <main className="page">
      <TopBar back="/kids" title="Your family" />
      <p className="quiet" style={{ margin: 0 }}>
        Kids practice under your sign-in. A profile keeps only a first name or nickname, a picture colour and a grade:
        no email, photo or location.
      </p>
      {kids.map((kid) => (
        <KidRow key={kid.id} kid={kid} />
      ))}
      {kids.length < 8 ? (
        <div className="card">
          <h2 style={{ fontSize: 20 }}>Add a kid</h2>
          <KidForm
            key={formKey}
            submitLabel="Add kid"
            busy={add.isPending}
            error={add.error?.message}
            onSubmit={(body) => add.mutate(body, { onSuccess: () => setFormKey((k) => k + 1) })}
          />
        </div>
      ) : (
        <p className="quiet">A family can have up to 8 kids.</p>
      )}
      <p className="quiet" style={{ fontSize: 14 }}>
        Signed in as {family.data?.displayName} ({family.data?.email}).
      </p>
    </main>
  )
}

export function ManageFamilyPage() {
  return (
    <SignedInOnly>
      <Manage />
    </SignedInOnly>
  )
}
