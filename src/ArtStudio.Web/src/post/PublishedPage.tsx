import { Link } from 'react-router-dom'
import { api } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { PUBLISHED_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { usePagedItems } from '../paging/usePagedItems'

export function PublishedPage() {
  const sets = useLoad(() => api.sets('published'))
  useStudioEvents(['SetUpdated', 'SetDeleted'], sets.reload)
  const filter = useListFilter('published', sets.data, PUBLISHED_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

  return (
    <div>
      <div className="toolbar">
        <h2>Published</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {sets.data?.length === 0 && <p className="hint">Nothing published yet.</p>}
      {sets.data && sets.data.length > 0 && (
        <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} filter={filter} />
      )}
      {sets.data && sets.data.length > 0 && filter.items?.length === 0 && (
        <p className="hint">No sets match this filter.</p>
      )}
      <div className="set-grid">
        {paged.pageItems.map((set) => (
          <div key={set.id} className="panel set-card">
            <Link to={`/sets/${set.id}`}>
              {set.previewImageUrl ? (
                <img className="set-card-image" src={set.previewImageUrl} alt="" loading="lazy" />
              ) : (
                <div className="set-card-image" />
              )}
            </Link>
            <div className="set-card-body">
              <div className="job-meta">
                <Link to={`/sets/${set.id}`}>#{set.id}</Link>
                <span>
                  {set.pickedCount} {set.pickedCount === 1 ? 'pick' : 'picks'}
                </span>
                <a className="badge status-Completed" href={set.publishedUrl!} target="_blank" rel="noreferrer">
                  Posted
                </a>
                {set.publishedAt && <span>{new Date(set.publishedAt).toLocaleString()}</span>}
                {set.deviantArtAuthor && (
                  <span>
                    inspired by <DeviantArtUserLink username={set.deviantArtAuthor} />
                  </span>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
      {pager}
    </div>
  )
}
