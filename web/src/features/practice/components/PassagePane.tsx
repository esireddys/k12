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
 * The question with its selection: side by side when there is room (a tablet in landscape, a laptop), otherwise
 * the selection first with a button to hide it, then the question.
 */
export function WithPassage({ passage, children }: { passage?: PassageView; children: React.ReactNode }) {
  const [hidden, setHidden] = useState(false)
  if (!passage) return <>{children}</>
  return (
    <div className="reading-layout">
      <div className="reading-passage">
        <button type="button" className="passage-toggle" onClick={() => setHidden((h) => !h)} aria-expanded={!hidden}>
          <Icon name="book" size={18} /> {hidden ? `Show “${passage.title}”` : 'Hide the selection'}
        </button>
        {!hidden && <PassagePane passage={passage} />}
      </div>
      <div className="reading-question">{children}</div>
    </div>
  )
}
