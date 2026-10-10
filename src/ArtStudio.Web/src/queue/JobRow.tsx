import { Link } from 'react-router-dom'
import type { Job } from '../api'
import { useReturnState } from '../sets/returnTo'
import { useJobActions } from './useJobActions'

interface Props {
  job: Job
  showPrompt?: boolean
}

export function JobRow({ job, showPrompt = true }: Props) {
  const actions = useJobActions(job)
  const returnState = useReturnState()

  return (
    <div className="panel job">
      <Link to={`/sets/${job.setId}`} state={returnState}>
        {job.sourceImageUrl ? <img className="job-thumb" src={job.sourceImageUrl} alt="" /> : <div className="job-thumb" />}
      </Link>
      <div>
        {showPrompt && (
          <Link to={`/sets/${job.setId}`} state={returnState} className="job-prompt plain-link">
            {job.prompt}
          </Link>
        )}
        <div className="job-meta">
          <span className={`badge status-${job.status}`}>{job.status}</span>
          <span>
            {job.completedCount}/{job.requestedCount} images
          </span>
          <div className="progress" aria-hidden>
            <div style={{ width: `${actions.percent}%` }} />
          </div>
          <span>{job.resolution}</span>
          <span>set #{job.setId}</span>
        </div>
        {job.error && <div className="job-error">{job.error}</div>}
        {actions.error && <div className="job-error">{actions.error}</div>}
      </div>
      <div className="job-actions">
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
      </div>
    </div>
  )
}
