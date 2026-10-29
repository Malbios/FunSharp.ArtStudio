import { api, type Job } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { Pager } from '../paging/Pager'
import { usePagedItems } from '../paging/usePagedItems'
import { JobTile } from './JobTile'
import { PromptQueueSection } from './PromptQueueSection'
import { queueSections, type QueueSection } from './queueSections'

export function QueuePage() {
  const queue = useLoad(api.queue)

  useStudioEvents(['JobUpdated', 'QueueStateChanged', 'SetDeleted'], queue.reload)

  const data = queue.data
  const allPaused = data !== undefined && data.imagesPaused && data.videosPaused

  return (
    <div>
      <div className="toolbar">
        <h2>Queue</h2>
        {data && (
          <button type="button" onClick={() => void (allPaused ? api.resumeQueue() : api.pauseQueue())}>
            {allPaused ? 'Resume all' : 'Pause all'}
          </button>
        )}
      </div>

      {queue.error && <p className="error">{queue.error}</p>}
      {data && queueSections(data).map((section) => <QueueSectionView key={section.kind} section={section} />)}
      <PromptQueueSection />
    </div>
  )
}

function QueueSectionView({ section }: { section: QueueSection }) {
  const paged = usePagedItems(section.jobs, `${section.kind.toLowerCase()}Page`)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />
  const pendingCount = section.jobs.filter((job) => job.status !== 'Failed').length
  const hasFailure = section.jobs.some((job) => job.status === 'Failed')
  const noun = section.title.toLowerCase()

  // Jobs only move among the waiting jobs of their own kind, the way they run.
  const waitingIds = section.jobs.filter((job) => job.status === 'Queued').map((job) => job.id)
  const position = (job: Job) => waitingIds.indexOf(job.id)

  return (
    <section className="queue-section" aria-label={section.title}>
      <div className="toolbar queue-section-toolbar">
        <h3>
          {section.title} <span className="hint">{pendingCount > 0 ? `${pendingCount} waiting or running` : 'nothing queued'}</span>
        </h3>
        <button
          type="button"
          onClick={() => void (section.paused ? api.resumeQueue(section.kind) : api.pauseQueue(section.kind))}
        >
          {section.paused ? `Resume ${noun}` : `Pause ${noun}`}
        </button>
      </div>

      {section.paused && (
        <div className="queue-banner">
          <span>
            {section.title} are paused.{' '}
            {hasFailure && 'A job failed: retry it to continue with it first, or resume to skip it.'}
          </span>
          <button type="button" onClick={() => void api.resumeQueue(section.kind)}>
            Resume
          </button>
        </div>
      )}
      {section.waitingNote && <p className="hint">{section.waitingNote}</p>}

      {pager}
      <div className="job-grid">
        {paged.pageItems.map((job) => (
          <JobTile
            key={job.id}
            job={job}
            canMoveUp={position(job) > 0}
            canMoveDown={position(job) >= 0 && position(job) < waitingIds.length - 1}
          />
        ))}
      </div>
      {pager}
    </section>
  )
}
