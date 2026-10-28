import type { SetSummary } from '../api'

export function NewVideosBadge({ set }: { set: SetSummary }) {
  const count = set.unviewedVideoCount
  if (count === 0) return null
  return (
    <span className="badge new-videos" title="Videos you haven't played yet">
      {count} new {count === 1 ? 'video' : 'videos'}
    </span>
  )
}
