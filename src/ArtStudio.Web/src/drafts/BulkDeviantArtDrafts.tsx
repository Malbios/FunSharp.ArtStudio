import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { parseDeviantArtUrls } from './parseUrls'

type BulkResult =
  | { url: string; status: 'pending' }
  | { url: string; status: 'added'; setId: number }
  | { url: string; status: 'failed'; error: string; existingSetId: number | null }

export function BulkDeviantArtDrafts() {
  const [text, setText] = useState('')
  const [results, setResults] = useState<BulkResult[]>([])
  const [running, setRunning] = useState(false)
  const urls = parseDeviantArtUrls(text)
  const doneCount = results.filter((result) => result.status !== 'pending').length

  async function addAll() {
    setRunning(true)
    setText('')
    setResults(urls.map((url) => ({ url, status: 'pending' })))
    for (const [index, url] of urls.entries()) {
      const result = await addOne(url)
      setResults((previous) => previous.map((existing, i) => (i === index ? result : existing)))
    }
    setRunning(false)
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
        {running && (
          <span className="hint">
            Adding {Math.min(doneCount + 1, results.length)} of {results.length}…
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
                <span className="success">
                  Added as <Link to={`/?draft=${result.setId}`}>draft #{result.setId}</Link>
                </span>
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
