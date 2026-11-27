import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { Icon } from '../../../shared/ui/Icon'

export function SessionHeader({ done, total, children }: { done: number; total: number; children?: ReactNode }) {
  return (
    <div className="topbar">
      <Link to="/home" className="icon-button" aria-label="Quit">
        <Icon name="close" />
      </Link>
      <div className="progress" role="progressbar" aria-valuemin={0} aria-valuemax={total} aria-valuenow={done}>
        <div style={{ width: `${total ? (done / total) * 100 : 0}%` }} />
      </div>
      <div style={{ fontWeight: 800 }}>
        {done} / {total}
      </div>
      {children}
    </div>
  )
}
