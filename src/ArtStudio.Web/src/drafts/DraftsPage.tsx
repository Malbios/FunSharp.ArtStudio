import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { Pager } from '../paging/Pager'
import { usePagedItems } from '../paging/usePagedItems'

export function DraftsPage() {
  const drafts = useLoad(() => api.sets('draft'))
  const [error, setError] = useState<string>()
  useStudioEvents(['SetUpdated', 'SetDeleted'], drafts.reload)
  const paged = usePagedItems(drafts.data)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

  async function deleteDraft(setId: number) {
    if (!window.confirm(`Delete draft #${setId}?`)) return
    setError(undefined)
    try {
      await api.deleteSet(setId)
      drafts.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  return (
    <div>
      <div className="toolbar">
        <h2>Drafts</h2>
      </div>
      {drafts.error && <p className="error">{drafts.error}</p>}
      {error && <p className="error">{error}</p>}
      {drafts.data?.length === 0 && (
        <p className="hint">
          No drafts. Attach an image on the <Link to="/">New</Link> page and use "Save as draft" to add a prompt later.
        </p>
      )}
      {pager}
      <div className="set-grid">
        {paged.pageItems.map((draft) => (
          <div key={draft.id} className="panel set-card">
            <Link to={`/?draft=${draft.id}`} title="Add a prompt and queue">
              {draft.sourceImageUrl ? (
                <img className="set-card-image" src={draft.sourceImageUrl} alt="" loading="lazy" />
              ) : (
                <div className="set-card-image" />
              )}
            </Link>
            <div className="set-card-body">
              <div className="job-meta">
                <Link to={`/?draft=${draft.id}`}>#{draft.id}</Link>
                {draft.deviantArtAuthor && <DeviantArtUserLink username={draft.deviantArtAuthor} />}
                <button type="button" className="link-button" onClick={() => void deleteDraft(draft.id)}>
                  Delete
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
      {pager}
    </div>
  )
}
