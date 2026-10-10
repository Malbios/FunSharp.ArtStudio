import { Link } from 'react-router-dom'
import { api } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { SET_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { usePagedItems } from '../paging/usePagedItems'
import { useReturnState } from './returnTo'

export function SetsPage() {
  const sets = useLoad(() => api.sets('working'))
  useStudioEvents(['JobUpdated', 'ImageAdded', 'SetDeleted', 'SetUpdated'], sets.reload)
  const filter = useListFilter('sets', sets.data, SET_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />
  const returnState = useReturnState()

  return (
    <div>
      <div className="toolbar">
        <h2>Sets</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {sets.data?.length === 0 && (
        <p className="hint">
          No sets yet. <Link to="/">Create one</Link>.
        </p>
      )}
      {sets.data && sets.data.length > 0 && (
        <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} filter={filter} />
      )}
      {sets.data && sets.data.length > 0 && filter.items?.length === 0 && (
        <p className="hint">No sets match this filter.</p>
      )}
      <div className="set-grid">
        {paged.pageItems.map((set) => {
          const thumbnail = set.previewImageUrl ?? set.sourceImageUrl
          return (
            <Link key={set.id} to={`/sets/${set.id}`} state={returnState} className="panel set-card">
              {thumbnail ? <img className="set-card-image" src={thumbnail} alt="" loading="lazy" /> : <div className="set-card-image" />}
              <div className="set-card-body">
                <div className="job-prompt">{set.prompt}</div>
                <div className="job-meta">
                  <span>#{set.id}</span>
                  <span>{set.imageCount} images</span>
                  {set.hasActiveJob && <span className="badge status-Running">generating</span>}
                  {set.hasFailedJob && <span className="badge status-Failed">failed</span>}
                  {set.pickedCount > 0 && <span className="badge status-Completed">{set.pickedCount} picked</span>}
                </div>
              </div>
            </Link>
          )
        })}
      </div>
      {pager}
    </div>
  )
}
