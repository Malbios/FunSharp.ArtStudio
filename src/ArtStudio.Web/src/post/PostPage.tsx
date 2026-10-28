import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { POST_FILTERS } from '../paging/listFilters'
import { Pager } from '../paging/Pager'
import { useListFilter } from '../paging/useListFilter'
import { PAGE_PARAM, usePagedItems } from '../paging/usePagedItems'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { NewVideosBadge } from '../sets/NewVideosBadge'

const PRESET_PARAM = 'preset'

export function PostPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const presetId = Number(searchParams.get(PRESET_PARAM)) || undefined
  const sets = useLoad(() => api.sets('ready', presetId), presetId)
  const blocks = useLoad(api.buildingBlocks)
  const presets = blocks.data?.filter((block) => block.kind === 'Preset' && block.artStyle) ?? []
  useStudioEvents(['SetUpdated', 'SetDeleted'], sets.reload)
  const unstashed = sets.data?.filter((set) => !set.stashed)
  const filter = useListFilter('post', unstashed, POST_FILTERS)
  const paged = usePagedItems(filter.items)
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />

  function choosePreset(value: string) {
    setSearchParams((params) => {
      params.delete(PAGE_PARAM)
      if (value) params.set(PRESET_PARAM, value)
      else params.delete(PRESET_PARAM)
      return params
    })
  }

  const presetSelect = presets.length > 0 && (
    <select aria-label="Preset" value={presetId ?? ''} onChange={(e) => choosePreset(e.target.value)}>
      <option value="">All presets</option>
      {presets.map((preset) => (
        <option key={preset.id} value={preset.id}>
          {preset.label}
        </option>
      ))}
    </select>
  )

  return (
    <div>
      <div className="toolbar">
        <h2>Post</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {unstashed?.length === 0 &&
        (presetId ? (
          <p className="hint">No sets waiting for the Stash have picks from this preset.</p>
        ) : (
          <p className="hint">
            Nothing is waiting to go to the Stash. Pick images in a set, then use "Ready to post" on its page.
          </p>
        ))}
      {unstashed && (unstashed.length > 0 || presetId) && (
        <Pager
          page={paged.page}
          pageCount={paged.pageCount}
          onChange={paged.setPage}
          filter={filter}
          tools={presetSelect}
        />
      )}
      {unstashed && unstashed.length > 0 && filter.items?.length === 0 && (
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
                <NewVideosBadge set={set} />
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
