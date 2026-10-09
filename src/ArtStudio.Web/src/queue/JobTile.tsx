import { Link } from 'react-router-dom'
import type { Job } from '../api'
import { useJobActions } from './useJobActions'

export function JobTile({ job }: { job: Job }) {
  const actions = useJobActions(job)
  const setUrl = `/sets/${job.setId}`

  return (
    <div className="panel job-tile">
      <Link to={setUrl} className="job-tile-image">
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
        <Link to={setUrl} className="button-link">
          Open set
        </Link>
      </div>
    </div>
  )
}
