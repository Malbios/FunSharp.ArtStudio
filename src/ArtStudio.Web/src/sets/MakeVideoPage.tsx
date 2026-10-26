import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api, type ImageInfo } from '../api'
import { useLoad } from '../live/useLoad'
import { initialVideoForm, resolveVideoForm, type VideoForm } from '../video/videoForm'
import { VideoOptionsFields } from '../video/VideoOptionsFields'

/** Queues videos of a set that start from one of its images, and optionally end on another. */
export function MakeVideoPage() {
  const setId = Number(useParams().id)
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  // The set page's own state, so going back to it still knows where the set was opened from.
  const setPageState: unknown = useLocation().state
  const set = useLoad(() => api.set(setId), setId)
  const options = useLoad(api.videoOptions)
  const [editedForm, setForm] = useState<VideoForm>()
  const form = editedForm ?? (options.data && initialVideoForm(options.data))
  const [chosenStartId, setStartId] = useState<number>()
  const [endImageId, setEndImageId] = useState<number | null>(null)
  const [queueing, setQueueing] = useState(false)
  const [error, setError] = useState<string>()

  // Opened from the set page's viewer, which may have been scrolled far down.
  useEffect(() => window.scrollTo(0, 0), [])

  const images = set.data?.images ?? []
  const startId = chosenStartId ?? (Number(searchParams.get('start')) || images[0]?.id)
  const start = images.find((image) => image.id === startId)
  const end = images.find((image) => image.id === endImageId)
  const backToSet = () => navigate(`/sets/${setId}`, { state: setPageState })

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!form || !options.data || !start) return
    const resolved = resolveVideoForm(form, options.data)
    if (resolved.error !== undefined) return setError(resolved.error)

    setError(undefined)
    setQueueing(true)
    try {
      await api.makeVideo(setId, start.id, end?.id ?? null, resolved.settings)
      backToSet()
    } catch (failure) {
      setError((failure as Error).message)
      setQueueing(false)
    }
  }

  if (set.error) return <p className="error">{set.error}</p>
  if (!set.data) return <p className="hint">Loading…</p>
  if (set.data.archivedAt)
    return (
      <p className="hint">
        Set #{setId} is archived. Restore it first to make videos. <Link to={`/sets/${setId}`}>Back to the set</Link>
      </p>
    )

  return (
    <form className="panel make-video" onSubmit={(e) => void submit(e)}>
      <div className="toolbar">
        <h2>Make video from set #{setId}</h2>
        <Link to={`/sets/${setId}`} state={setPageState}>
          Back to the set
        </Link>
      </div>

      <div className="video-frames">
        <figure>
          {start ? <img src={start.url} alt="Start frame" /> : <div className="video-frame-empty">None</div>}
          <figcaption>Start</figcaption>
        </figure>
        <span className="video-frames-arrow" aria-hidden>
          →
        </span>
        <figure>
          {end ? <img src={end.url} alt="End frame" /> : <div className="video-frame-empty">Free</div>}
          <figcaption>End</figcaption>
        </figure>
      </div>

      <FrameChooser label="Start frame" images={images} chosenId={startId ?? null} onChoose={(id) => setStartId(id!)} />
      <FrameChooser
        label="End frame (optional)"
        images={images.filter((image) => image.id !== startId)}
        chosenId={endImageId}
        allowNone
        onChoose={setEndImageId}
      />

      {options.error && <p className="error">{options.error}</p>}
      {form && options.data && (
        <VideoOptionsFields form={form} options={options.data} disabled={queueing} onChange={setForm} />
      )}

      <div className="field-row submit-row">
        {form && (
          <label className="submit-in-row inline-check" title="Put it ahead of the waiting videos; waiting images still run first">
            <input
              type="checkbox"
              checked={form.queueFirst}
              disabled={queueing}
              onChange={(e) => setForm({ ...form, queueFirst: e.target.checked })}
            />
            Queue first among the videos
          </label>
        )}
        <button type="submit" className="primary submit-in-row" disabled={queueing || !form || !start}>
          {queueing ? 'Queueing…' : 'Add to queue'}
        </button>
      </div>
      {error && <p className="error">{error}</p>}
    </form>
  )
}

interface FrameChooserProps {
  label: string
  images: ImageInfo[]
  chosenId: number | null
  allowNone?: boolean
  onChoose: (imageId: number | null) => void
}

function FrameChooser({ label, images, chosenId, allowNone = false, onChoose }: FrameChooserProps) {
  return (
    <div className="field">
      <span>{label}</span>
      <div className="video-frame-choices" role="radiogroup" aria-label={label}>
        {allowNone && (
          <button
            type="button"
            role="radio"
            aria-checked={chosenId === null}
            className={chosenId === null ? 'chosen' : undefined}
            onClick={() => onChoose(null)}
          >
            None
          </button>
        )}
        {images.map((image, index) => (
          <button
            key={image.id}
            type="button"
            role="radio"
            aria-checked={chosenId === image.id}
            aria-label={`Image ${index + 1}, seed ${image.seed}`}
            className={chosenId === image.id ? 'chosen' : undefined}
            onClick={() => onChoose(image.id)}
          >
            <img src={image.url} alt="" loading="lazy" />
          </button>
        ))}
      </div>
    </div>
  )
}
