import { useState } from 'react'
import { api } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { JobTile } from './JobTile'

export function QueuePage() {
  const queue = useLoad(api.queue)
  const [showRecent, setShowRecent] = useState(false)

  useStudioEvents(['JobUpdated', 'QueueStateChanged', 'SetDeleted'], queue.reload)

  async function togglePause() {
    if (queue.data?.paused) await api.resumeQueue()
    else await api.pauseQueue()
  }

  const data = queue.data
  const pendingCount = data?.active.filter((job) => job.status !== 'Failed').length ?? 0

  return (
    <div>
      <div className="toolbar">
        <h2>Queue</h2>
        {data && (
          <button type="button" onClick={() => void togglePause()}>
            {data.paused ? 'Resume queue' : 'Pause queue'}
          </button>
        )}
      </div>

      {queue.error && <p className="error">{queue.error}</p>}

      {data?.paused && (
        <div className="queue-banner">
          <span>
            The queue is paused.{' '}
            {data.active.some((job) => job.status === 'Failed') &&
              'A job failed: retry it to continue with it first, or resume to skip it.'}
          </span>
          <button type="button" onClick={() => void api.resumeQueue()}>
            Resume
          </button>
        </div>
      )}

      {data && pendingCount > 0 && <p className="hint">{pendingCount} job(s) waiting or running.</p>}
      {data && data.active.length === 0 && <p className="hint">Nothing queued.</p>}
      <div className="job-grid">
        {data?.active.map((job) => (
          <JobTile key={job.id} job={job} />
        ))}
      </div>

      {data && data.recent.length > 0 && (
        <>
          <button type="button" className="link-button" onClick={() => setShowRecent(!showRecent)}>
            {showRecent ? 'Hide' : 'Show'} recently finished ({data.recent.length})
          </button>
          {showRecent && (
            <div className="job-grid recent">
              {data.recent.map((job) => (
                <JobTile key={job.id} job={job} />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}
