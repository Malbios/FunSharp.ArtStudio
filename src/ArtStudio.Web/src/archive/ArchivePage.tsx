import { Link } from 'react-router-dom'
import { api } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { ARCHIVED_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { usePagedItems } from '../paging/usePagedItems'

export function ArchivePage() {
  const sets = useLoad(() => api.sets('archived'))
  useStudioEvents(['SetUpdated', 'SetDeleted'], sets.reload)
  const filter = useListFilter('archived', sets.data, ARCHIVED_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

  return (
    <div>
      <div className="toolbar">
        <h2>Archived</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {sets.data?.length === 0 && (
        <p className="hint">
          Nothing archived. Archived sets keep their DeviantArt link, so the same deviation is still reported as used.
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
            <div key={set.id} className="panel set-card">
              <Link to={`/sets/${set.id}`}>
                {thumbnail ? (
                  <img className="set-card-image" src={thumbnail} alt="" loading="lazy" />
                ) : (
                  <div className="set-card-image" />
                )}
              </Link>
              <div className="set-card-body">
                <div className="job-meta">
                  <Link to={`/sets/${set.id}`}>#{set.id}</Link>
                  {set.publishedUrl ? (
                    <a className="badge status-Completed" href={set.publishedUrl} target="_blank" rel="noreferrer">
                      Posted
                    </a>
                  ) : (
                    set.readyToPostAt && <span className="badge status-Completed">was ready to post</span>
                  )}
                  {set.deviantArtAuthor && (
                    <span>
                      inspired by <DeviantArtUserLink username={set.deviantArtAuthor} />
                    </span>
                  )}
                </div>
              </div>
            </div>
          )
        })}
      </div>
      {pager}
    </div>
  )
}
