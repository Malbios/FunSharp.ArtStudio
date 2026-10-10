import { Link } from 'react-router-dom'
import type { Job, QueueMove } from '../api'
import { useReturnState } from '../sets/returnTo'
import { useJobActions } from './useJobActions'

const MOVES: { move: QueueMove; label: string; title: string; earlier: boolean }[] = [
  { move: 'Top', label: '⤒ Top', title: 'Run next, before all other waiting jobs', earlier: true },
  { move: 'Up', label: '↑ Up', title: 'Move one place earlier in the queue', earlier: true },
  { move: 'Down', label: '↓ Down', title: 'Move one place later in the queue', earlier: false },
  { move: 'Bottom', label: '⤓ Bottom', title: 'Move after all other waiting jobs', earlier: false },
]

interface Props {
  job: Job
  canMoveUp?: boolean
  canMoveDown?: boolean
}

export function JobTile({ job, canMoveUp = false, canMoveDown = false }: Props) {
  const actions = useJobActions(job)
  const setUrl = `/sets/${job.setId}`
  const returnState = useReturnState()

  return (
    <div className="panel job-tile">
      <Link to={setUrl} state={returnState} className="job-tile-image">
        {job.sourceImageUrl ? (
          <img src={job.sourceImageUrl} alt={`Base image of set #${job.setId}`} loading="lazy" />
        ) : (
          <div className="job-tile-placeholder">
            <span>No base image</span>
            <span>{job.resolution}</span>
          </div>
        )}
        {job.prompt.trim() && <div className="job-tile-prompt">{job.prompt}</div>}
      </Link>

      <div className="job-tile-details">
        <div className="job-meta">
          <span className={`badge status-${job.status}`}>{job.status}</span>
          <span>
            {job.completedCount}/{job.requestedCount} images
          </span>
        </div>
        <div className="progress wide" aria-hidden>
          <div style={{ width: `${actions.percent}%` }} />
        </div>
        <div className="job-meta">
          <span>{job.resolution}</span>
          <span>set #{job.setId}</span>
        </div>
        {job.error && <div className="job-error">{job.error}</div>}
        {actions.error && <div className="job-error">{actions.error}</div>}
      </div>

      <div className="job-tile-controls">
        {actions.canMove && (
          <div className="job-tile-move">
            {MOVES.map(({ move, label, title, earlier }) => (
              <button
                key={move}
                type="button"
                disabled={actions.busy || !(earlier ? canMoveUp : canMoveDown)}
                title={title}
                onClick={() => actions.move(move)}
              >
                {label}
              </button>
            ))}
          </div>
        )}
        {actions.canRetry && (
          <button type="button" className="primary" disabled={actions.busy} onClick={actions.retry}>
            Retry
          </button>
        )}
        {actions.canCancel && (
          <button type="button" className="danger" disabled={actions.busy} onClick={actions.cancel}>
            Cancel
          </button>
        )}
        <Link to={setUrl} state={returnState} className="button-link">
          Open set
        </Link>
      </div>
    </div>
  )
}
