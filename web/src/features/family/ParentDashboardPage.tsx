import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Avatar } from '../../shared/ui/Avatar'
import { Button } from '../../shared/ui/Button'
import { TopBar } from '../../shared/ui/TopBar'
import { useFamily } from './api/useFamily'
import { useKidDashboard, useLock, useParentGate, type KidDashboard, type SkillStanding } from './api/useParentArea'
import { ParentGate } from './components/ParentGate'
import { PrivacyNote } from './components/PrivacyNote'
import { SetPinCard } from './components/SetPinCard'
import { SignedInOnly } from './components/SignedInOnly'

const pct = (part: number | string, whole: number | string) => (Number(whole) ? Math.round((100 * Number(part)) / Number(whole)) : 0)
const day = (iso: string) => new Date(iso).toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric' })

const levelText: Record<SkillStanding['level'], string> = {
  NotStarted: 'Not started',
  Learning: 'Learning',
  Practicing: 'Practicing',
  Mastered: 'Mastered',
}

function Tile({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="card" style={{ gap: 2, padding: 18 }}>
      <div className="quiet" style={{ fontSize: 14, fontWeight: 700 }}>
        {label}
      </div>
      <div style={{ fontSize: 30, fontWeight: 800 }}>{value}</div>
      {note && (
        <div className="quiet" style={{ fontSize: 14 }}>
          {note}
        </div>
      )}
    </div>
  )
}

function SkillRow({ skill }: { skill: SkillStanding }) {
  const weak = skill.level !== 'Mastered' && skill.accuracy != null && Number(skill.accuracy) < 70
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        gap: 12,
        padding: '12px 14px',
        borderRadius: 12,
        background: weak ? '#FFF7ED' : 'var(--bg)',
        fontSize: 16,
      }}
    >
      <span>
        <b>{skill.code}</b> {skill.description}
      </span>
      <span style={{ fontWeight: 700, color: weak ? 'var(--miss-ink)' : 'var(--ink-quiet)', whiteSpace: 'nowrap' }}>
        {Number(skill.answered) > 0 ? `${skill.accuracy}% · ${skill.answered} tries · ${levelText[skill.level]}` : 'Not started'}
      </span>
    </div>
  )
}

function Dashboard({ d }: { d: KidDashboard }) {
  const [allSkills, setAllSkills] = useState(false)
  const exam = d.lastExam
  const change = exam?.previousPercent != null ? Number(exam.percent) - Number(exam.previousPercent) : null
  const skills = d.skills.filter((s) => s.hasQuestions)

  return (
    <>
      <div className="grid-4">
        <Tile label="This week" value={`${d.week.daysPracticed} ${Number(d.week.daysPracticed) === 1 ? 'day' : 'days'}`} note={`${d.week.minutes} min practiced`} />
        <Tile label="Questions answered" value={String(d.answered)} note={Number(d.answered) ? `${pct(d.correct, d.answered)}% correct` : 'None yet'} />
        <Tile
          label="Last practice test"
          value={exam ? `${exam.percent}%` : '–'}
          note={
            exam
              ? `${exam.title}${change == null ? '' : change === 0 ? ', same as before' : `, ${change > 0 ? 'up' : 'down'} from ${exam.previousPercent}%`}`
              : 'No full test yet'
          }
        />
        <Tile label="Skills mastered" value={`${d.skillsMastered} / ${d.skillsTotal}`} note={d.frameworkName ?? d.courseName ?? undefined} />
      </div>

      <div className="row" style={{ alignItems: 'stretch', gap: 16 }}>
        <div className="card" style={{ flex: '2 1 380px', gap: 10 }}>
          <h2 style={{ fontSize: 18 }}>Needs attention</h2>
          {d.needsAttention.length === 0 && <p className="quiet">Nothing stands out yet. More practice will show where to focus.</p>}
          {d.needsAttention.map((s) => (
            <SkillRow key={s.code} skill={s} />
          ))}
          {Number(d.mistakesToFix) > 0 && (
            <p style={{ margin: 0 }}>
              {d.name} has <b>{d.mistakesToFix}</b> missed {Number(d.mistakesToFix) === 1 ? 'question' : 'questions'} waiting in “Fix my mistakes”.
            </p>
          )}
          <Button variant="ghost" style={{ alignSelf: 'flex-start' }} onClick={() => setAllSkills((v) => !v)} aria-expanded={allSkills}>
            {allSkills ? 'Hide skills' : `See all ${skills.length} skills`}
          </Button>
          {allSkills && skills.map((s) => <SkillRow key={s.code} skill={s} />)}
        </div>

        <div className="card" style={{ flex: '1 1 280px', gap: 10 }}>
          <h2 style={{ fontSize: 18 }}>Recent practice</h2>
          <div className="row" style={{ gap: 8 }}>
            <span className="chip">{d.streak}-day streak</span>
            <span className="chip">Best {d.bestStreak}</span>
          </div>
          {d.recentSessions.length === 0 && <p className="quiet">No practice yet.</p>}
          {d.recentSessions.map((s) => (
            <div key={s.id} style={{ display: 'flex', justifyContent: 'space-between', gap: 8, fontSize: 15 }}>
              <span>
                <b>{s.title}</b>
                <span className="quiet">
                  {' '}
                  · {s.mode === 'Exam' ? 'Test' : 'Practice'} · {day(s.startedAt)}
                </span>
              </span>
              <b style={{ whiteSpace: 'nowrap' }}>
                {s.correct}/{s.total}
              </b>
            </div>
          ))}
        </div>
      </div>
    </>
  )
}

function Parent() {
  const navigate = useNavigate()
  const family = useFamily()
  const gate = useParentGate()
  const lock = useLock()
  const kids = family.data?.kids ?? []
  const [picked, setPicked] = useState<string | null>(null)
  const kidId = picked ?? kids[0]?.id ?? null
  const dashboard = useKidDashboard(kidId)

  const done = () => lock.mutate(undefined, { onSuccess: () => navigate('/kids') })

  return (
    <main className="page" style={{ gap: 20 }}>
      <TopBar back="/kids" title="Parent dashboard">
        {gate.data?.hasPin && (
          <Button variant="dark" onClick={done} disabled={lock.isPending}>
            Lock and go back
          </Button>
        )}
      </TopBar>

      {kids.length === 0 ? (
        <p>
          No kids yet. <Link to="/family">Add your first kid</Link>.
        </p>
      ) : (
        <div className="row" role="tablist" aria-label="Kids">
          {kids.map((k) => (
            <Button key={k.id} role="tab" aria-selected={k.id === kidId} variant={k.id === kidId ? 'primary' : 'secondary'} onClick={() => setPicked(k.id)}>
              <span className="row" style={{ gap: 8 }}>
                <Avatar name={k.name} color={k.avatar} size={28} /> {k.name}
              </span>
            </Button>
          ))}
        </div>
      )}

      {dashboard.isError && <p role="alert">{dashboard.error.message}</p>}
      {dashboard.isPending && kidId && <p>Loading…</p>}
      {dashboard.data && <Dashboard d={dashboard.data} />}

      <div className="row" style={{ alignItems: 'stretch', gap: 16 }}>
        <div className="card" style={{ flex: '1 1 320px', gap: 12 }}>
          <h2 style={{ fontSize: 18 }}>Settings</h2>
          <div className="row" style={{ justifyContent: 'space-between' }}>
            <span>Kids, names and grades</span>
            <Link to="/family" style={{ fontWeight: 800 }}>
              Manage family
            </Link>
          </div>
          {gate.data && <SetPinCard hasPin={gate.data.hasPin} />}
        </div>
        <div style={{ flex: '1 1 320px' }}>
          <PrivacyNote />
        </div>
      </div>
    </main>
  )
}

/** The parent dashboard: how each kid is doing, behind the parent PIN. */
export function ParentDashboardPage() {
  return (
    <SignedInOnly>
      <ParentGate>
        <Parent />
      </ParentGate>
    </SignedInOnly>
  )
}
