import { api } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { Pager } from '../paging/Pager'
import { usePagedItems } from '../paging/usePagedItems'
import { JobTile } from './JobTile'

export function QueuePage() {
  const queue = useLoad(api.queue)

  useStudioEvents(['JobUpdated', 'QueueStateChanged', 'SetDeleted'], queue.reload)

  async function togglePause() {
    if (queue.data?.paused) await api.resumeQueue()
    else await api.pauseQueue()
  }

  const data = queue.data
  const pendingCount = data?.active.filter((job) => job.status !== 'Failed').length ?? 0
  const waitingIds = data?.active.filter((job) => job.status === 'Queued').map((job) => job.id) ?? []
  const paged = usePagedItems(data?.active)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

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
      {pager}
      <div className="job-grid">
        {paged.pageItems.map((job) => (
          <JobTile
            key={job.id}
            job={job}
            canMoveUp={waitingIds.indexOf(job.id) > 0}
            canMoveDown={waitingIds.includes(job.id) && waitingIds.indexOf(job.id) < waitingIds.length - 1}
          />
        ))}
      </div>
      {pager}
    </div>
  )
}
