import { useEffect, useState } from 'react'
import { Icon } from '../../../shared/ui/Icon'

/**
 * A printed picture (an item or a page) that fills the width it has and opens full screen when tapped, so small
 * print and figures stay readable on a phone. Full screen it shows at its own size and can be scrolled and pinched.
 */
export function ZoomablePicture({ src, alt, maxHeight, onBroken }: { src: string; alt: string; maxHeight?: string; onBroken?: () => void }) {
  const [open, setOpen] = useState(false)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  return (
    <>
      <button type="button" className="zoomable" onClick={() => setOpen(true)} aria-label={`${alt}. Tap to make it bigger.`}>
        <img src={src} alt={alt} onError={onBroken} style={{ maxHeight }} />
        <span className="zoom-hint" aria-hidden="true">
          <Icon name="zoom" size={18} />
        </span>
      </button>
      {open && (
        <div role="dialog" aria-modal="true" aria-label={alt} className="zoom-overlay" onClick={() => setOpen(false)}>
          <button type="button" className="icon-button zoom-close" aria-label="Close" onClick={() => setOpen(false)} autoFocus>
            <Icon name="close" />
          </button>
          <img src={src} alt={alt} onClick={(e) => e.stopPropagation()} />
        </div>
      )}
    </>
  )
}
