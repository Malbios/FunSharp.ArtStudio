import { useEffect, useRef, useState, type FormEvent, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'
import { api, type ImageInfo } from '../api'
import { useLoad } from '../live/useLoad'
import { initialVideoForm, resolveVideoForm, type VideoForm } from '../video/videoForm'
import { VideoOptionsFields } from '../video/VideoOptionsFields'

interface Props {
  setId: number
  images: ImageInfo[]
  start: ImageInfo
  onQueued: () => void
  onClose: () => void
}

/** Queues videos that start from one of the set's images, and optionally end on another. */
export function VideoDialog({ setId, images, start, onQueued, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const options = useLoad(api.videoOptions)
  const [editedForm, setForm] = useState<VideoForm>()
  const form = editedForm ?? (options.data && initialVideoForm(options.data))
  const [endImageId, setEndImageId] = useState<number | null>(null)
  const [queueing, setQueueing] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  // Portalled, but React still bubbles the click to the opener; the lightbox would close on it.
  function closeOnBackdropClick(event: MouseEvent) {
    event.stopPropagation()
    if (event.target === dialogRef.current) onClose()
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    event.stopPropagation()
    if (!form || !options.data) return
    const resolved = resolveVideoForm(form, options.data)
    if (resolved.error !== undefined) return setError(resolved.error)

    setError(undefined)
    setQueueing(true)
    try {
      await api.makeVideo(setId, start.id, endImageId, resolved.settings)
      onQueued()
      onClose()
    } catch (failure) {
      setError((failure as Error).message)
      setQueueing(false)
    }
  }

  return createPortal(
    <dialog ref={dialogRef} className="modify-dialog video-dialog" onClose={onClose} onClick={closeOnBackdropClick}>
      <form onSubmit={(e) => void submit(e)}>
        <h3>Make video</h3>
        <div className="video-frames">
          <figure>
            <img src={start.url} alt="Start frame" />
            <figcaption>Start</figcaption>
          </figure>
          <span className="video-frames-arrow" aria-hidden>
            →
          </span>
          <figure>
            {endImageId === null ? (
              <div className="video-frame-empty">Free</div>
            ) : (
              <img src={images.find((image) => image.id === endImageId)?.url} alt="End frame" />
            )}
            <figcaption>End</figcaption>
          </figure>
        </div>
        <div className="field">
          <span>End frame (optional)</span>
          <div className="video-end-choices" role="radiogroup" aria-label="End frame">
            <button
              type="button"
              role="radio"
              aria-checked={endImageId === null}
              className={endImageId === null ? 'chosen' : undefined}
              onClick={() => setEndImageId(null)}
            >
              None
            </button>
            {images
              .filter((image) => image.id !== start.id)
              .map((image) => (
                <button
                  key={image.id}
                  type="button"
                  role="radio"
                  aria-checked={endImageId === image.id}
                  aria-label={`End on image seed ${image.seed}`}
                  className={endImageId === image.id ? 'chosen' : undefined}
                  onClick={() => setEndImageId(image.id)}
                >
                  <img src={image.url} alt="" loading="lazy" />
                </button>
              ))}
          </div>
        </div>

        {options.error && <p className="error">{options.error}</p>}
        {form && options.data && (
          <VideoOptionsFields form={form} options={options.data} disabled={queueing} onChange={setForm} />
        )}
        {form && (
          <label className="inline-check" title="Put it ahead of the waiting videos; waiting images still run first">
            <input
              type="checkbox"
              checked={form.queueFirst}
              disabled={queueing}
              onChange={(e) => setForm({ ...form, queueFirst: e.target.checked })}
            />
            Queue first among the videos
          </label>
        )}

        {error && <p className="error">{error}</p>}
        <div className="modify-dialog-actions">
          <button type="button" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="primary" disabled={queueing || !form}>
            {queueing ? 'Queueing…' : 'Add to queue'}
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  )
}
