import { useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent, type Ref } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { MESSAGE_DURATION_MS } from '../messages'
import { emptyRow, spreadPastedLinks, waitingUrls, withTrailingEmptyRow, type BulkRow, type WaitingUrl } from './bulkRows'
import { parseDeviantArtUrls } from './parseUrls'

type BulkResult =
  | { url: string; status: 'pending' }
  | { url: string; status: 'added'; setId: number }
  | { url: string; status: 'failed'; error: string; existingSetId: number | null }

interface Progress {
  done: number
  total: number
}

const ADD_RIGHT_AWAY_KEY = 'artstudio.drafts.addRightAway'
const MAX_VISION_CONTEXT_LENGTH = 1000

function rememberedAddRightAway(): boolean {
  try {
    return localStorage.getItem(ADD_RIGHT_AWAY_KEY) === 'true'
  } catch {
    return false
  }
}

function rememberAddRightAway(on: boolean) {
  try {
    localStorage.setItem(ADD_RIGHT_AWAY_KEY, String(on))
  } catch {
    // Without storage the choice is simply not remembered.
  }
}

export function BulkDeviantArtDrafts() {
  const [rows, setRows] = useState<BulkRow[]>(() => [emptyRow()])
  const [results, setResults] = useState<BulkResult[]>([])
  const [progress, setProgress] = useState<Progress>()
  const [addRightAway, setAddRightAway] = useState(rememberedAddRightAway)
  const firstUrlRef = useRef<HTMLInputElement>(null)
  const waitingRef = useRef<WaitingUrl[]>([])
  const workingRef = useRef(false)
  const filledCount = waitingUrls(rows).length

  function enqueue(items: WaitingUrl[]) {
    const fresh = items.filter(
      (item, index) =>
        items.findIndex((other) => other.url === item.url) === index &&
        !waitingRef.current.some((waiting) => waiting.url === item.url),
    )
    if (fresh.length === 0) return
    const freshUrls = fresh.map((item) => item.url)
    waitingRef.current.push(...fresh)
    setResults((previous) => [
      ...previous.filter((existing) => !freshUrls.includes(existing.url)),
      ...freshUrls.map((url): BulkResult => ({ url, status: 'pending' })),
    ])
    setProgress((previous) => ({ done: previous?.done ?? 0, total: (previous?.total ?? 0) + fresh.length }))
    if (!workingRef.current) void addWaiting()
  }

  // One at a time, so DeviantArt never sees parallel downloads from this box.
  async function addWaiting() {
    workingRef.current = true
    while (waitingRef.current.length > 0) {
      const { url, visionContext } = waitingRef.current[0]
      const result = await addOne(url, visionContext)
      waitingRef.current.shift()
      setResults((previous) => previous.map((existing) => (existing.url === url ? result : existing)))
      setProgress((previous) => previous && { ...previous, done: previous.done + 1 })
    }
    workingRef.current = false
    setProgress(undefined)
  }

  function addRows(toAdd: BulkRow[]) {
    enqueue(waitingUrls(toAdd))
    setRows([emptyRow()])
    firstUrlRef.current?.focus()
  }

  function changeRow(index: number, change: Partial<BulkRow>) {
    const next = rows.map((row, i) => (i === index ? { ...row, ...change } : row))
    setRows(addRightAway ? next : withTrailingEmptyRow(next))
  }

  function removeRow(index: number) {
    setRows(withTrailingEmptyRow(rows.filter((_, i) => i !== index)))
  }

  function pasteLinks(index: number, event: ClipboardEvent<HTMLInputElement>) {
    const urls = parseDeviantArtUrls(event.clipboardData.getData('text'))
    if (urls.length === 0 || (!addRightAway && urls.length === 1)) return
    event.preventDefault()
    const spread = spreadPastedLinks(rows, index, urls)
    if (addRightAway) addRows(spread)
    else setRows(withTrailingEmptyRow(spread))
  }

  function addOnEnter(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter' && addRightAway) addRows(rows)
  }

  function toggleAddRightAway(on: boolean) {
    setAddRightAway(on)
    rememberAddRightAway(on)
    if (on) addRows(rows)
    else setRows(withTrailingEmptyRow(rows))
  }

  function removeResult(url: string) {
    setResults((previous) => previous.filter((result) => result.url !== url))
  }

  function removeFailedResults() {
    setResults((previous) => previous.filter((result) => result.status !== 'failed'))
  }

  const failedCount = results.filter((result) => result.status === 'failed').length

  async function addOne(url: string, visionContext?: string): Promise<BulkResult> {
    try {
      const { id } = await api.addDeviantArtDraft(url, visionContext)
      return { url, status: 'added', setId: id }
    } catch (failure) {
      const existingSetId = failure instanceof ApiError ? failure.existingSetId : null
      return { url, status: 'failed', error: (failure as Error).message, existingSetId }
    }
  }

  return (
    <section className="panel bulk-drafts">
      <h3>Bulk add from DeviantArt</h3>
      <div className="bulk-drafts-rows">
        {rows.map((row, index) => (
          <BulkLinkRow
            key={row.id}
            row={row}
            urlRef={index === 0 ? firstUrlRef : undefined}
            removable={!addRightAway && index < rows.length - 1}
            onChange={(change) => changeRow(index, change)}
            onPaste={(event) => pasteLinks(index, event)}
            onKeyDown={addOnEnter}
            onRemove={() => removeRow(index)}
          />
        ))}
      </div>
      <div className="bulk-drafts-actions">
        <button type="button" className="primary" disabled={filledCount === 0} onClick={() => addRows(rows)}>
          {filledCount > 1 ? `Add ${filledCount} drafts` : 'Add draft'}
        </button>
        <label className="inline-check" title="Add a link as a draft as soon as it is pasted, or on Enter">
          <input type="checkbox" checked={addRightAway} onChange={(e) => toggleAddRightAway(e.target.checked)} />
          Add each URL right away
        </label>
        {progress && (
          <span className="hint">
            Adding {Math.min(progress.done + 1, progress.total)} of {progress.total}…
          </span>
        )}
      </div>
      {results.length > 0 && (
        <ul className="bulk-results">
          {results.map((result) => (
            <li key={result.url}>
              <span className="bulk-result-url">{result.url}</span>
              {result.status === 'pending' && <span className="hint">waiting…</span>}
              {result.status === 'added' && (
                <AddedNote setId={result.setId} onExpire={() => removeResult(result.url)} />
              )}
              {result.status === 'failed' && (
                <>
                  <span className="error">
                    {result.error}{' '}
                    {result.existingSetId !== null && (
                      <Link to={`/sets/${result.existingSetId}`}>Open set #{result.existingSetId}</Link>
                    )}
                  </span>
                  <button
                    type="button"
                    className="link-button bulk-result-dismiss"
                    aria-label={`Dismiss the error for ${result.url}`}
                    title="Dismiss"
                    onClick={() => removeResult(result.url)}
                  >
                    ✕
                  </button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
      {failedCount > 1 && (
        <button type="button" className="link-button bulk-results-dismiss-all" onClick={removeFailedResults}>
          Dismiss all {failedCount} errors
        </button>
      )}
    </section>
  )
}

interface BulkLinkRowProps {
  row: BulkRow
  urlRef?: Ref<HTMLInputElement>
  removable: boolean
  onChange: (change: Partial<BulkRow>) => void
  onPaste: (event: ClipboardEvent<HTMLInputElement>) => void
  onKeyDown: (event: KeyboardEvent<HTMLInputElement>) => void
  onRemove: () => void
}

function BulkLinkRow({ row, urlRef, removable, onChange, onPaste, onKeyDown, onRemove }: BulkLinkRowProps) {
  return (
    <div className="bulk-drafts-row">
      <input
        ref={urlRef}
        type="text"
        value={row.url}
        onChange={(e) => onChange({ url: e.target.value })}
        onPaste={onPaste}
        onKeyDown={onKeyDown}
        aria-label="DeviantArt link"
        placeholder="DeviantArt link"
      />
      <input
        type="text"
        value={row.visionContext}
        maxLength={MAX_VISION_CONTEXT_LENGTH}
        onChange={(e) => onChange({ visionContext: e.target.value })}
        aria-label="Context for the vision model"
        placeholder="Optional context for the vision model, e.g. who a person is"
        title="Optional context for the vision model, e.g. who a person is, or details the image alone does not show"
      />
      <button
        type="button"
        className={removable ? 'link-button' : 'link-button bulk-drafts-row-spacer'}
        aria-label="Remove this link"
        title="Remove"
        disabled={!removable}
        onClick={onRemove}
      >
        ✕
      </button>
    </div>
  )
}

function AddedNote({ setId, onExpire }: { setId: number; onExpire: () => void }) {
  const onExpireRef = useRef(onExpire)

  useEffect(() => {
    onExpireRef.current = onExpire
  })

  useEffect(() => {
    const timer = setTimeout(() => onExpireRef.current(), MESSAGE_DURATION_MS)
    return () => clearTimeout(timer)
  }, [])

  return (
    <span className="success">
      Added as <Link to={`/?draft=${setId}`}>draft #{setId}</Link>
    </span>
  )
}
