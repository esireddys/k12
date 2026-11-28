import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Icon } from './Icon'

/** A screen's header: an optional back link, the title, and anything on the right. */
export function TopBar({ back, title, children }: { back?: string; title?: ReactNode; children?: ReactNode }) {
  return (
    <div className="topbar">
      {back && (
        <Link to={back} className="icon-button" aria-label="Back">
          <Icon name="back" />
        </Link>
      )}
      {title && <h1>{title}</h1>}
      {children && <div className="spacer row">{children}</div>}
    </div>
  )
}
