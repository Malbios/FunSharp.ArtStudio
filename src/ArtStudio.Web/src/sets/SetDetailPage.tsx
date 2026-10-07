import { useCallback, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
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
  const [deleting, setDeleting] = useState(false)
  const [deletedElsewhere, setDeletedElsewhere] = useState(false)
  const navigate = useNavigate()

  useStudioEvents(['JobUpdated', 'ImageAdded', 'SetDeleted'], (event, payload) => {
    const affectsThisSet = event === 'Reconnected' || (payload as JobEventPayload).setId === setId
    if (!affectsThisSet) return
    if (event === 'SetDeleted') setDeletedElsewhere(true)
    else set.reload()
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

  async function deleteSet() {
    const confirmed = window.confirm(
      `Delete set #${setId}? Image files stay in the folder. Queued or running jobs of this set are cancelled.`,
    )
    if (!confirmed) return
    setDeleting(true)
    setError(undefined)
    try {
      await api.deleteSet(setId)
      navigate('/sets')
    } catch (failure) {
      setError((failure as Error).message)
      setDeleting(false)
    }
  }


  if (deletedElsewhere && !deleting)
    return (
      <p className="hint">
        This set was deleted. <Link to="/sets">Back to all sets</Link>
      </p>
    )
  if (set.error) return <p className="error">{set.error}</p>
  if (!set.data) return <p className="hint">Loading…</p>

  const data = set.data
  const hasInspiration = Boolean(data.sourceImageUrl || data.deviantArtUrl)
  const activeJobs = data.jobs.filter((job) => job.status === 'Queued' || job.status === 'Running' || job.status === 'Failed')

  return (
    <div>
      <div className="toolbar set-toolbar">
        <h2>Set #{data.id}</h2>
        <form className="inline-form" onSubmit={(e) => void requestMore(e)}>
          <input
            type="number"
            min={1}
            max={100}
            value={moreCount}
            aria-label="Number of additional images"
            onChange={(e) => setMoreCount(Math.max(1, Number(e.target.value) || 1))}
          />
          <button type="submit" className="primary">
            More images
          </button>
        </form>
        <Link to={`/?basedOn=${data.id}`} className="button-link inline">
          Edit &amp; requeue
        </Link>
        <button type="button" className="danger" disabled={deleting} onClick={() => void deleteSet()}>
          {deleting ? 'Deleting…' : 'Delete set'}
        </button>
        <Link to="/sets">All sets</Link>
      </div>
      {error && <p className="error">{error}</p>}

      <div className={hasInspiration ? 'set-compare' : 'set-compare single'}>
        {hasInspiration && (
          <section className="set-inspiration-column">
            {data.sourceImageUrl && (
              <a href={data.sourceImageUrl} target="_blank" rel="noreferrer">
                <img className="set-inspiration" src={data.sourceImageUrl} alt="Inspiration image" />
              </a>
            )}
            {data.deviantArtUrl && (
              <p className="hint">
                Inspired by{' '}
                <a href={data.deviantArtUrl} target="_blank" rel="noreferrer">
                  {data.deviantArtUrl}
                </a>
                {data.deviantArtAuthor && ` (${data.deviantArtAuthor})`}
              </p>
            )}
          </section>
        )}

        <section>
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
        </section>
      </div>


      {activeJobs.length > 0 && (
        <div className="job-list">
          {activeJobs.map((job) => (
            <JobRow key={job.id} job={job} showPrompt={false} />
          ))}
        </div>
      )}

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
