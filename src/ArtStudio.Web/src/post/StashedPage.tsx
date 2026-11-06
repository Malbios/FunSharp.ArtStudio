import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type Gallery, type SetSummary } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { NewVideosBadge } from '../sets/NewVideosBadge'
import { useReturnState } from '../sets/returnTo'
import { initialPublishChoice } from './postDefaults'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { STASHED_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { usePagedItems } from '../paging/usePagedItems'

export function StashedPage() {
  const sets = useLoad(() => api.sets('ready'))
  useStudioEvents(['SetUpdated', 'SetDeleted'], sets.reload)
  const galleries = useLoad(() => api.galleries())
  const stashed = sets.data?.filter((set) => set.stashed)
  const filter = useListFilter('stashed', stashed, STASHED_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />
  const returnState = useReturnState()

  return (
    <div>
      <div className="toolbar">
        <h2>Stashed</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {galleries.error && <p className="error">No galleries: {galleries.error}</p>}
      {stashed?.length === 0 && <p className="hint">Nothing in the Stash. Upload a set from its page on Post.</p>}
      {stashed && stashed.length > 0 && (
        <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} filter={filter} />
      )}
      {stashed && stashed.length > 0 && filter.items?.length === 0 && <p className="hint">No sets match this filter.</p>}
      <div className="set-grid">
        {paged.pageItems.map((set) => (
          <div key={set.id} className="panel set-card">
            <Link to={`/sets/${set.id}`} state={returnState}>
              {set.previewImageUrl ? (
                <img className="set-card-image" src={set.previewImageUrl} alt="" loading="lazy" />
              ) : (
                <div className="set-card-image" />
              )}
            </Link>
            <div className="set-card-body">
              <div className="job-meta">
                <Link to={`/sets/${set.id}`} state={returnState}>#{set.id}</Link>
                <span>
                  {set.pickedCount} {set.pickedCount === 1 ? 'pick' : 'picks'}
                </span>
                <NewVideosBadge set={set} />
                {set.deviantArtAuthor && (
                  <span>
                    inspired by <DeviantArtUserLink username={set.deviantArtAuthor} />
                  </span>
                )}
              </div>
              <PublishFromCard set={set} galleries={galleries.data} onPublished={sets.reload} />
            </div>
          </div>
        ))}
      </div>
      {pager}
    </div>
  )
}

interface PublishFromCardProps {
  set: SetSummary
  galleries: Gallery[] | undefined
  onPublished: () => void
}

/** Publishes right away to the gallery the set's publish form would start at. */
function PublishFromCard({ set, galleries, onPublished }: PublishFromCardProps) {
  const [publishing, setPublishing] = useState(false)
  const [error, setError] = useState<string>()
  const { galleryId, matureLevel } = initialPublishChoice(galleries ?? [], set)
  const gallery = galleries?.find((candidate) => candidate.folderId === galleryId)

  async function publish() {
    setPublishing(true)
    setError(undefined)
    try {
      const isMature = matureLevel !== 'none'
      await api.publish(set.id, galleryId, isMature, isMature ? matureLevel : null)
      onPublished()
    } catch (failure) {
      setError((failure as Error).message)
      setPublishing(false)
    }
  }

  return (
    <div className="set-card-publish">
      {galleries && (
        <p className="hint">
          {gallery
            ? `To ${gallery.name}${matureLevel === 'none' ? '' : `, mature (${matureLevel})`}`
            : 'No gallery chosen yet. Choose one on the set page.'}
        </p>
      )}
      <button type="button" className="success" disabled={publishing || !gallery} onClick={() => void publish()}>
        {publishing ? 'Publishing…' : 'Publish'}
      </button>
      {error && <p className="error">{error}</p>}
    </div>
  )
}
