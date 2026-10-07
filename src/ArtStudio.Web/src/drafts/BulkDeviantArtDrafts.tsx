import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { MESSAGE_DURATION_MS } from '../messages'
import { parseDeviantArtUrls } from './parseUrls'

type BulkResult =
  | { url: string; status: 'pending' }
  | { url: string; status: 'added'; setId: number }
  | { url: string; status: 'failed'; error: string; existingSetId: number | null }

interface Progress {
  done: number
  total: number
}

export function BulkDeviantArtDrafts() {
  const [text, setText] = useState('')
  const [results, setResults] = useState<BulkResult[]>([])
  const [progress, setProgress] = useState<Progress>()
  const urls = parseDeviantArtUrls(text)
  const running = progress !== undefined

  async function addAll() {
    setProgress({ done: 0, total: urls.length })
    setText('')
    setResults(urls.map((url) => ({ url, status: 'pending' })))
    for (const [index, url] of urls.entries()) {
      const result = await addOne(url)
      setResults((previous) => previous.map((existing) => (existing.url === url ? result : existing)))
      setProgress({ done: index + 1, total: urls.length })
    }
    setProgress(undefined)
  }

  function removeResult(url: string) {
    setResults((previous) => previous.filter((result) => result.url !== url))
  }

  async function addOne(url: string): Promise<BulkResult> {
    try {
      const { id } = await api.addDeviantArtDraft(url)
      return { url, status: 'added', setId: id }
    } catch (failure) {
      const existingSetId = failure instanceof ApiError ? failure.existingSetId : null
      return { url, status: 'failed', error: (failure as Error).message, existingSetId }
    }
  }

  return (
    <section className="panel bulk-drafts">
      <h3>Bulk add from DeviantArt</h3>
      <textarea
        rows={4}
        value={text}
        disabled={running}
        onChange={(e) => setText(e.target.value)}
        placeholder="Paste DeviantArt URLs separated by spaces or line breaks. Each becomes a draft with the post's main image."
      />
      <div className="bulk-drafts-actions">
        <button type="button" className="primary" disabled={running || urls.length === 0} onClick={() => void addAll()}>
          {urls.length > 1 ? `Add ${urls.length} drafts` : 'Add draft'}
        </button>
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
                <span className="error">
                  {result.error}{' '}
                  {result.existingSetId !== null && (
                    <Link to={`/sets/${result.existingSetId}`}>Open set #{result.existingSetId}</Link>
                  )}
                </span>
              )}
            </li>
          ))}
        </ul>
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
