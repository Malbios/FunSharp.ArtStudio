import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type PromptQueueItem } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { Pager } from '../paging/Pager'
import { usePagedItems } from '../paging/usePagedItems'
import { MOVES } from './queueMoves'

export function PromptQueueSection() {
  const prompts = useLoad(api.promptQueue)
  useStudioEvents(['SetUpdated', 'SetDeleted'], prompts.reload)
  const items = prompts.data ?? []
  const paged = usePagedItems(prompts.data, 'promptsPage')
  const pager = <Pager page={paged.page} pageCount={paged.pageCount} onChange={paged.setPage} />
  const waitingIds = items.filter((item) => item.state === 'Queued').map((item) => item.setId)
  const position = (item: PromptQueueItem) => waitingIds.indexOf(item.setId)

  return (
    <section className="queue-section" aria-label="Prompts">
      <div className="toolbar queue-section-toolbar">
        <h3>
          Prompts{' '}
          <span className="hint">{items.length > 0 ? `${items.length} waiting or running` : 'nothing queued'}</span>
        </h3>
      </div>
      {prompts.error && <p className="error">{prompts.error}</p>}
      {pager}
      <div className="job-grid">
        {paged.pageItems.map((item) => (
          <PromptTile
            key={item.setId}
            item={item}
            canMoveUp={position(item) > 0}
            canMoveDown={position(item) >= 0 && position(item) < waitingIds.length - 1}
          />
        ))}
      </div>
      {pager}
    </section>
  )
}

interface PromptTileProps {
  item: PromptQueueItem
  canMoveUp: boolean
  canMoveDown: boolean
}

function PromptTile({ item, canMoveUp, canMoveDown }: PromptTileProps) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const openUrl = item.isDraft ? `/?draft=${item.setId}` : `/sets/${item.setId}`
  const label = `${item.isDraft ? 'draft' : 'set'} #${item.setId}`

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(undefined)
    try {
      await action()
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="panel job-tile">
      <Link to={openUrl} className="job-tile-image">
        {item.sourceImageUrl ? (
          <img src={item.sourceImageUrl} alt={`Inspiration image of ${label}`} loading="lazy" />
        ) : (
          <div className="job-tile-placeholder">No inspiration image</div>
        )}
        {item.note && <div className="job-tile-prompt">{item.note}</div>}
      </Link>

      <div className="job-tile-details">
        <div className="job-meta">
          <span className={`badge status-${item.state}`}>{item.state}</span>
          <span>{item.kind === 'Modify' ? 'Modify prompt' : 'New prompt'}</span>
          <span>{label}</span>
        </div>
        {item.note && <div className="hint prompt-queue-note">{item.note}</div>}
        {error && <div className="job-error">{error}</div>}
      </div>

      <div className="job-tile-controls">
        {item.state === 'Queued' && (
          <div className="job-tile-move">
            {MOVES.map(({ move, label: moveLabel, title, earlier }) => (
              <button
                key={move}
                type="button"
                disabled={busy || !(earlier ? canMoveUp : canMoveDown)}
                title={title}
                onClick={() => void run(() => api.movePrompt(item.setId, move))}
              >
                {moveLabel}
              </button>
            ))}
          </div>
        )}
        <button
          type="button"
          className="danger"
          disabled={busy}
          onClick={() => void run(() => api.cancelPrompt(item.setId))}
        >
          Cancel
        </button>
        <Link to={openUrl} className="button-link">
          Open
        </Link>
      </div>
    </div>
  )
}
