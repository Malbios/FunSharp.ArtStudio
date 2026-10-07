import { useCallback, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type ImageInfo } from '../api'
import { useStudioEvents, type JobEventPayload } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { JobRow } from '../queue/JobRow'
import { Lightbox } from './Lightbox'

const DEFAULT_MORE_COUNT = 2

export function SetDetailPage() {
  const setId = Number(useParams().id)
  const set = useLoad(() => api.set(setId), setId)
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null)
  const [moreCount, setMoreCount] = useState(DEFAULT_MORE_COUNT)
  const [error, setError] = useState<string>()
  const [copied, setCopied] = useState(false)

  useStudioEvents(['JobUpdated', 'ImageAdded'], (event, payload) => {
    if (event === 'Reconnected' || (payload as JobEventPayload).setId === setId) set.reload()
  })

  const closeLightbox = useCallback(() => setLightboxIndex(null), [])

  async function requestMore(event: FormEvent) {
    event.preventDefault()
    setError(undefined)
    try {
      await api.moreImages(setId, moreCount)
      set.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function toggleSelect(image: ImageInfo) {
    const nextSelection = set.data?.selectedImageId === image.id ? null : image.id
    try {
      await api.selectImage(setId, nextSelection)
      set.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function copyPrompt(prompt: string) {
    await navigator.clipboard.writeText(prompt)
    setCopied(true)
    setTimeout(() => setCopied(false), 1200)
  }

  if (set.error) return <p className="error">{set.error}</p>
  if (!set.data) return <p className="hint">Loading…</p>

  const data = set.data
  const activeJobs = data.jobs.filter((job) => job.status === 'Queued' || job.status === 'Running' || job.status === 'Failed')

  return (
    <div>
      <div className="toolbar">
        <h2>Set #{data.id}</h2>
        <Link to="/sets">All sets</Link>
      </div>

      <div className="set-header">
        <div className="panel">
          <p className="set-prompt">{data.prompt}</p>
          <div className="job-meta">
            <span>{data.resolution}</span>
            <span>{new Date(data.createdAt).toLocaleString()}</span>
            <span>{data.images.length} images</span>
            <button type="button" className="link-button" onClick={() => void copyPrompt(data.prompt)}>
              {copied ? 'Copied!' : 'Copy prompt'}
            </button>
          </div>
          {data.deviantArtUrl && (
            <p className="hint">
              Inspired by{' '}
              <a href={data.deviantArtUrl} target="_blank" rel="noreferrer">
                {data.deviantArtUrl}
              </a>
              {data.deviantArtAuthor && ` (${data.deviantArtAuthor})`}
            </p>
          )}
          {data.sourceImageUrl && (
            <a href={data.sourceImageUrl} target="_blank" rel="noreferrer">
              <img className="source-preview set-source" src={data.sourceImageUrl} alt="Base image" />
            </a>
          )}
        </div>

        <div className="panel set-actions">
          <form className="inline-form" onSubmit={(e) => void requestMore(e)}>
            <input
              type="number"
              min={1}
              max={100}
              value={moreCount}
              onChange={(e) => setMoreCount(Math.max(1, Number(e.target.value) || 1))}
            />
            <button type="submit" className="primary">
              More images
            </button>
          </form>
          <Link to={`/?basedOn=${data.id}`} className="button-link">
            Edit &amp; requeue
          </Link>
          {error && <p className="error">{error}</p>}
        </div>
      </div>

      {activeJobs.length > 0 && (
        <div className="job-list">
          {activeJobs.map((job) => (
            <JobRow key={job.id} job={job} showPrompt={false} />
          ))}
        </div>
      )}

      <div className="image-grid">
        {data.images.map((image, index) => (
          <button
            key={image.id}
            type="button"
            className={image.id === data.selectedImageId ? 'image-tile selected' : 'image-tile'}
            onClick={() => setLightboxIndex(index)}
          >
            {image.id === data.selectedImageId && <span className="badge">Picked</span>}
            <img src={image.url} alt={`Generated image ${index + 1}`} loading="lazy" />
          </button>
        ))}
      </div>
      {data.images.length === 0 && <p className="hint">No images yet.</p>}

      {lightboxIndex !== null && data.images[lightboxIndex] && (
        <Lightbox
          images={data.images}
          index={lightboxIndex}
          sourceImageUrl={data.sourceImageUrl}
          selectedImageId={data.selectedImageId}
          onIndexChange={setLightboxIndex}
          onToggleSelect={(image) => void toggleSelect(image)}
          onClose={closeLightbox}
        />
      )}
    </div>
  )
}
