import { useState } from 'react'
import { Icon } from '../../../shared/ui/Icon'
import { passagePageUrl, type PassageView } from '../api/usePractice'
import { ZoomablePicture } from './ZoomablePicture'

/**
 * A reading selection as printed: every page as a picture, which keeps the layout, underlined words and
 * illustrations the questions point at. It scrolls on its own, so the student can look back while answering.
 */
export function PassagePane({ passage }: { passage: PassageView }) {
  const pages = Array.from({ length: Number(passage.pageCount) }, (_, i) => i + 1)
  return (
    <section className="passage-pane card" aria-label={`Reading selection: ${passage.title}`}>
      <div className="row" style={{ gap: 10 }}>
        <Icon name="book" size={24} color="var(--accent)" />
        <h2 style={{ margin: 0, fontSize: 20 }}>{passage.title}</h2>
      </div>
      {pages.length === 0 && passage.text && <div style={{ whiteSpace: 'pre-line', fontSize: 19, lineHeight: 1.6 }}>{passage.text}</div>}
      {pages.map((n) => (
        <ZoomablePicture key={n} src={passagePageUrl(passage.id, n)} alt={`${passage.title}, page ${n} of ${pages.length}`} />
      ))}
    </section>
  )
}

/**
 * The question with its selection. Side by side when there is room (a tablet in landscape, a laptop). On a phone
 * or a portrait tablet there are two tabs, "Question" and the selection, opening on the question so a kid never
 * scrolls through pages of print to find it. Keyed by passage, so each new selection opens on the question again.
 */
export function WithPassage({ passage, children }: { passage?: PassageView; children: React.ReactNode }) {
  const [tab, setTab] = useState<'question' | 'passage'>('question')
  if (!passage) return <>{children}</>
  const tabs = [
    { id: 'question' as const, label: 'Question' },
    { id: 'passage' as const, label: `Read “${passage.title}”` },
  ]
  return (
    <div className="reading-layout" data-tab={tab}>
      <div className="reading-tabs" role="tablist" aria-label="Question or reading selection">
        {tabs.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            id={`reading-tab-${t.id}`}
            aria-selected={tab === t.id}
            aria-controls={`reading-${t.id}`}
            className="reading-tab"
            onClick={() => setTab(t.id)}
          >
            {t.id === 'passage' && <Icon name="book" size={18} />} {t.label}
          </button>
        ))}
      </div>
      <div className="reading-passage" id="reading-passage" role="tabpanel" aria-labelledby="reading-tab-passage">
        <PassagePane passage={passage} />
        <button type="button" className="reading-back" onClick={() => setTab('question')}>
          Back to the question
        </button>
      </div>
      <div className="reading-question" id="reading-question" role="tabpanel" aria-labelledby="reading-tab-question">
        {children}
      </div>
    </div>
  )
}
