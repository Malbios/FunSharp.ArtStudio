import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type Job } from '../api'

interface Props {
  job: Job
  showPrompt?: boolean
}

export function JobRow({ job, showPrompt = true }: Props) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const percent = job.requestedCount === 0 ? 0 : (job.completedCount / job.requestedCount) * 100

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(undefined)
    try {
      await action()
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const canCancel = job.status === 'Queued' || job.status === 'Running'

  return (
    <div className="panel job">
      <Link to={`/sets/${job.setId}`}>
        {job.sourceImageUrl ? <img className="job-thumb" src={job.sourceImageUrl} alt="" /> : <div className="job-thumb" />}
      </Link>
      <div>
        {showPrompt && (
          <Link to={`/sets/${job.setId}`} className="job-prompt plain-link">
            {job.prompt}
          </Link>
        )}
        <div className="job-meta">
          <span className={`badge status-${job.status}`}>{job.status}</span>
          <span>
            {job.completedCount}/{job.requestedCount} images
          </span>
          <div className="progress" aria-hidden>
            <div style={{ width: `${percent}%` }} />
          </div>
          <span>{job.resolution}</span>
          <span>set #{job.setId}</span>
        </div>
        {job.error && <div className="job-error">{job.error}</div>}
        {error && <div className="job-error">{error}</div>}
      </div>
      <div className="job-actions">
        {job.status === 'Failed' && (
          <button type="button" className="primary" disabled={busy} onClick={() => void run(() => api.retryJob(job.id))}>
            Retry
          </button>
        )}
        {canCancel && (
          <button type="button" className="danger" disabled={busy} onClick={() => void run(() => api.cancelJob(job.id))}>
            Cancel
          </button>
        )}
      </div>
    </div>
  )
}
