import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type SetDetail } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { NewVideosBadge } from '../sets/NewVideosBadge'
import { PublishForm } from './PostPanel'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { STASHED_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { usePagedItems } from '../paging/usePagedItems'

export function StashedPage() {
  const sets = useLoad(() => api.sets('ready'))
  useStudioEvents(['SetUpdated', 'SetDeleted'], sets.reload)
  const stashed = sets.data?.filter((set) => set.stashed)
  const filter = useListFilter('stashed', stashed, STASHED_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

  return (
    <div>
      <div className="toolbar">
        <h2>Stashed</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {stashed?.length === 0 && <p className="hint">Nothing in the Stash. Upload a set from its page on Post.</p>}
      {stashed && stashed.length > 0 && (
        <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} filter={filter} />
      )}
      {stashed && stashed.length > 0 && filter.items?.length === 0 && <p className="hint">No sets match this filter.</p>}
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
                <NewVideosBadge set={set} />
                {set.deviantArtAuthor && (
                  <span>
                    inspired by <DeviantArtUserLink username={set.deviantArtAuthor} />
                  </span>
                )}
              </div>
              <PublishFromCard setId={set.id} onPublished={sets.reload} />
            </div>
          </div>
        ))}
      </div>
      {pager}
    </div>
  )
}

function PublishFromCard({ setId, onPublished }: { setId: number; onPublished: () => void }) {
  const [set, setSet] = useState<SetDetail>()
  const [opening, setOpening] = useState(false)
  const [error, setError] = useState<string>()

  async function open() {
    setOpening(true)
    setError(undefined)
    try {
      setSet(await api.set(setId))
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setOpening(false)
    }
  }

  if (!set)
    return (
      <div className="set-card-publish">
        <button type="button" className="success" disabled={opening} onClick={() => void open()}>
          {opening ? 'Opening…' : 'Publish…'}
        </button>
        {error && <p className="error">{error}</p>}
      </div>
    )
  return (
    <div className="set-card-publish">
      <PublishForm
        set={set}
        onPublished={() => {
          setSet(undefined)
          onPublished()
        }}
      />
      <button type="button" className="link-button" onClick={() => setSet(undefined)}>
        Cancel
      </button>
    </div>
  )
}
