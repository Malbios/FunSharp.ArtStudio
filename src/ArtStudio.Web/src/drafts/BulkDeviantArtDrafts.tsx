import { useEffect, useRef, useState, type ClipboardEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { MESSAGE_DURATION_MS } from '../messages'
import { insertAtSelection } from '../prompt/insertAtSelection'
import { parseDeviantArtUrls, takeCompleteEntries } from './parseUrls'

type BulkResult =
  | { url: string; status: 'pending' }
  | { url: string; status: 'added'; setId: number }
  | { url: string; status: 'failed'; error: string; existingSetId: number | null }

interface Progress {
  done: number
  total: number
}

interface WaitingUrl {
  url: string
  visionContext?: string
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
  const [text, setText] = useState('')
  const [results, setResults] = useState<BulkResult[]>([])
  const [progress, setProgress] = useState<Progress>()
  const [addRightAway, setAddRightAway] = useState(rememberedAddRightAway)
  const [visionContext, setVisionContext] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const waitingRef = useRef<WaitingUrl[]>([])
  const workingRef = useRef(false)
  const urls = parseDeviantArtUrls(text)

  // The context describes one inspiration image, so only the first link added after typing it gets it.
  function enqueue(newUrls: string[]) {
    const fresh = newUrls.filter((url) => !waitingRef.current.some((waiting) => waiting.url === url))
    if (fresh.length === 0) return
    const context = visionContext.trim()
    waitingRef.current.push(...fresh.map((url, index) => ({ url, visionContext: index === 0 && context ? context : undefined })))
    if (context) setVisionContext('')
    setResults((previous) => [
      ...previous.filter((existing) => !fresh.includes(existing.url)),
      ...fresh.map((url): BulkResult => ({ url, status: 'pending' })),
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

  function addAll() {
    enqueue(urls)
    setText('')
    textareaRef.current?.focus()
  }

  function changeText(next: string) {
    if (!addRightAway) return setText(next)
    const { complete, rest } = takeCompleteEntries(next)
    setText(rest)
    enqueue(complete)
  }

  function toggleAddRightAway(on: boolean) {
    setAddRightAway(on)
    rememberAddRightAway(on)
    if (on) {
      const { complete, rest } = takeCompleteEntries(text)
      setText(rest)
      enqueue(complete)
    }
  }

  function pasteWithTrailingSpace(event: ClipboardEvent<HTMLTextAreaElement>) {
    const pasted = event.clipboardData.getData('text')
    if (!pasted || /\s$/.test(pasted)) return
    event.preventDefault()
    insertAtSelection(event.currentTarget, pasted + ' ')
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
      <label className="field">
        <span>
          Context for the vision model (optional) <em className="hint">Applies only to the next link you add.</em>
        </span>
        <input
          type="text"
          value={visionContext}
          maxLength={MAX_VISION_CONTEXT_LENGTH}
          onChange={(e) => setVisionContext(e.target.value)}
          placeholder="e.g. make sure she looks like the actress Emma Watson"
        />
      </label>
      <textarea
        ref={textareaRef}
        rows={4}
        value={text}
        onChange={(e) => changeText(e.target.value)}
        onPaste={pasteWithTrailingSpace}
        placeholder="Paste DeviantArt URLs separated by spaces or line breaks. Each becomes a draft with the post's main image."
      />
      <div className="bulk-drafts-actions">
        <button type="button" className="primary" disabled={urls.length === 0} onClick={addAll}>
          {urls.length > 1 ? `Add ${urls.length} drafts` : 'Add draft'}
        </button>
        <label className="inline-check" title="Add each URL as a draft as soon as a space or line break follows it">
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
