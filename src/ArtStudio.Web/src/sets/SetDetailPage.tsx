import { useCallback, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type ImageInfo } from '../api'
import { useStudioEvents, type JobEventPayload } from '../live/studioHub'
import { useLoad } from '../live/useLoad'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'
import { JobRow } from '../queue/JobRow'
import { Lightbox } from './Lightbox'
import { PickedStrip } from './PickedStrip'

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

  useStudioEvents(['JobUpdated', 'ImageAdded', 'SetDeleted', 'SetUpdated'], (event, payload) => {
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

  async function changePicks(action: () => Promise<void>) {
    setError(undefined)
    try {
      await action()
      set.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  function togglePick(image: ImageInfo) {
    const picked = set.data?.pickedImageIds.includes(image.id)
    void changePicks(() => (picked ? api.unpickImage(setId, image.id) : api.pickImage(setId, image.id)))
  }

  async function changeStage(action: () => Promise<void>, destination: string) {
    setError(undefined)
    try {
      await action()
      navigate(destination)
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
  const imagesById = new Map(data.images.map((image) => [image.id, image]))
  const pickedImages = data.pickedImageIds.flatMap((id) => imagesById.get(id) ?? [])
  const pickNumber = (image: ImageInfo) => data.pickedImageIds.indexOf(image.id) + 1
  const isReadyToPost = data.readyToPostAt !== null
  const readyBlocker =
    data.pickedImageIds.length === 0
      ? 'Pick at least one image first.'
      : data.jobs.some((job) => job.status === 'Queued' || job.status === 'Running')
        ? 'Wait for or cancel the queued and running jobs first.'
        : null

  const setActions = (
    <>
      {!isReadyToPost && (
        <>
          <form className="inline-form" onSubmit={(e) => void requestMore(e)}>
            <input
              type="number"
              min={1}
              max={100}
              value={moreCount}
              aria-label="Number of additional images"
              onChange={(e) => setMoreCount(Math.max(1, Number(e.target.value) || 1))}
            />
            <button type="submit">More images</button>
          </form>
          <Link to={`/?basedOn=${data.id}`} className="button-link inline">
            Edit &amp; requeue
          </Link>
          <button
            type="button"
            className="success"
            disabled={readyBlocker !== null}
            title={readyBlocker ?? 'Move this set to Post'}
            onClick={() => void changeStage(() => api.markReadyToPost(setId), '/post')}
          >
            Ready to post
          </button>
        </>
      )}
      <button type="button" className="danger" disabled={deleting} onClick={() => void deleteSet()}>
        {deleting ? 'Deleting…' : 'Delete set'}
      </button>
      <Link to={isReadyToPost ? '/post' : '/sets'}>{isReadyToPost ? 'All posts' : 'All sets'}</Link>
    </>
  )

  return (
    <div>
      <div className="toolbar set-toolbar">
        <h2>Set #{data.id}</h2>
        {setActions}
      </div>
      {error && <p className="error">{error}</p>}
      {isReadyToPost && (
        <div className="queue-banner ready-banner">
          <span>Ready to post since {new Date(data.readyToPostAt!).toLocaleString()}.</span>
          <button type="button" onClick={() => void changeStage(() => api.moveBackToSets(setId), '/sets')}>
            Back to sets
          </button>
        </div>
      )}

      <div className={hasInspiration ? 'set-compare' : 'set-compare single'}>
        <section>
          <div className="image-grid">
            {data.images.map((image, index) => {
              const number = pickNumber(image)
              return (
                <div key={image.id} className={number > 0 ? 'image-tile selected' : 'image-tile'}>
                  <button type="button" className="image-tile-open" onClick={() => setLightboxIndex(index)}>
                    <img src={image.url} alt={`Generated image ${index + 1}`} loading="lazy" />
                  </button>
                  <button
                    type="button"
                    className="image-tile-pick"
                    title={number > 0 ? 'Remove from picks' : 'Pick this image'}
                    onClick={() => togglePick(image)}
                  >
                    {number > 0 ? `★ #${number}` : '☆'}
                  </button>
                </div>
              )
            })}
          </div>
          {data.images.length === 0 && <p className="hint">No images yet.</p>}
        </section>

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
                {data.deviantArtAuthor && (
                  <>
                    {' '}
                    (<DeviantArtUserLink username={data.deviantArtAuthor} />)
                  </>
                )}
              </p>
            )}
          </section>
        )}
      </div>

      {activeJobs.length > 0 && (
        <div className="job-list">
          {activeJobs.map((job) => (
            <JobRow key={job.id} job={job} showPrompt={false} />
          ))}
        </div>
      )}

      <PickedStrip
        pickedImages={pickedImages}
        onReorder={(imageIds) => void changePicks(() => api.reorderPicks(setId, imageIds))}
        onUnpick={(image) => void changePicks(() => api.unpickImage(setId, image.id))}
        onOpen={(image) => setLightboxIndex(data.images.indexOf(image))}
      />

      {error && <p className="error">{error}</p>}
      <div className="toolbar set-toolbar set-toolbar-bottom">{setActions}</div>

      {lightboxIndex !== null && data.images[lightboxIndex] && (
        <Lightbox
          images={data.images}
          index={lightboxIndex}
          sourceImageUrl={data.sourceImageUrl}
          pickedImageIds={data.pickedImageIds}
          onIndexChange={setLightboxIndex}
          onTogglePick={togglePick}
          onClose={closeLightbox}
        />
      )}
    </div>
  )
}
