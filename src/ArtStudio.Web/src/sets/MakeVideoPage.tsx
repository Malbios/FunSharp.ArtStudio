import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { api, type VideoFrame } from '../api'
import { useLoad } from '../live/useLoad'
import { formFromVideo, initialVideoForm, resolveVideoForm, type Size, type VideoForm } from '../video/videoForm'
import { VideoOptionsFields, VideoPromptField } from '../video/VideoOptionsFields'
import { ImageViewer } from './ImageViewer'

interface ViewedFrame {
  url: string
  caption: string
}

interface LoadedSize {
  url: string
  size: Size
}

/** Accepts the older `?start=<imageId>` links as well as frame keys. */
function startKeyOf(param: string | null): string | undefined {
  if (!param) return undefined
  return /^\d+$/.test(param) ? `image:${param}` : param
}

/**
 * Queues videos of a set that start from one of its frames, and optionally end on another.
 * `?start=<frame key>` preselects the start frame; `?from=<videoId>` fills everything in from an earlier video.
 */
export function MakeVideoPage() {
  const setId = Number(useParams().id)
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  // The set page's own state, so going back to it still knows where the set was opened from.
  const setPageState: unknown = useLocation().state
  const set = useLoad(() => api.set(setId), setId)
  const frames = useLoad(() => api.videoFrames(setId), setId)
  const options = useLoad(api.videoOptions)
  const [editedForm, setForm] = useState<VideoForm>()
  const [chosenStartKey, setStartKey] = useState<string>()
  const [chosenEndKey, setEndKey] = useState<string | null>()
  const [queueing, setQueueing] = useState(false)
  const [error, setError] = useState<string>()
  const [startSize, setStartSize] = useState<LoadedSize>()
  const [viewed, setViewed] = useState<ViewedFrame>()

  // Opened from the set page's viewer, which may have been scrolled far down.
  useEffect(() => window.scrollTo(0, 0), [])

  const fromVideoId = Number(searchParams.get('from'))
  const fromVideo = fromVideoId ? set.data?.videos.find((video) => video.id === fromVideoId) : undefined
  const form =
    editedForm ??
    (options.data && (fromVideo ? formFromVideo(fromVideo, options.data) : initialVideoForm(options.data)))

  const allFrames = frames.data ?? []
  const frameOf = (key: string | null | undefined) => allFrames.find((frame) => frame.key === key)
  const start =
    frameOf(chosenStartKey) ??
    frameOf(fromVideo ? fromVideo.startFrameKey : startKeyOf(searchParams.get('start'))) ??
    allFrames[0]
  const end = frameOf(chosenEndKey === undefined ? fromVideo?.endFrameKey : chosenEndKey)
  const lostFrames =
    fromVideo !== undefined &&
    (fromVideo.startFrameKey === null || (fromVideo.endImageUrl !== null && fromVideo.endFrameKey === null))
  const closeViewer = useCallback(() => setViewed(undefined), [])
  const backToSet = () => navigate(`/sets/${setId}`, { state: setPageState })

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!form || !options.data || !start) return
    const resolved = resolveVideoForm(form, options.data)
    if (resolved.error !== undefined) return setError(resolved.error)

    setError(undefined)
    setQueueing(true)
    try {
      await api.makeVideo(setId, start.key, end?.key ?? null, resolved.settings)
      backToSet()
    } catch (failure) {
      setError((failure as Error).message)
      setQueueing(false)
    }
  }

  const loadError = set.error ?? frames.error
  if (loadError) return <p className="error">{loadError}</p>
  if (!set.data || !frames.data) return <p className="hint">Loading…</p>
  if (set.data.archivedAt)
    return (
      <p className="hint">
        Set #{setId} is archived. Restore it first to make videos. <Link to={`/sets/${setId}`}>Back to the set</Link>
      </p>
    )

  return (
    <form className="panel make-video" onSubmit={(e) => void submit(e)}>
      <div className="toolbar">
        <h2>{fromVideo ? `Make video #${fromVideo.id} again` : `Make video from set #${setId}`}</h2>
        <Link to={`/sets/${setId}`} state={setPageState}>
          Back to the set
        </Link>
      </div>

      {options.error && <p className="error">{options.error}</p>}
      {form && options.data && (
        <VideoPromptField form={form} options={options.data} disabled={queueing} onChange={setForm} />
      )}

      <div className="video-frames">
        <FramePreview
          frame={start}
          caption="Start"
          emptyText="None"
          onOpen={setViewed}
          onSize={(size) => start && setStartSize({ url: start.url, size })}
        />
        <span className="video-frames-arrow" aria-hidden>
          →
        </span>
        <FramePreview frame={end} caption="End" emptyText="Free" onOpen={setViewed} />
      </div>
      {lostFrames && <p className="hint">A frame of the earlier video is no longer in this set; choose another.</p>}

      <FrameChooser label="Start frame" frames={allFrames} chosenKey={start?.key ?? null} onChoose={(key) => setStartKey(key!)} />
      <FrameChooser
        label="End frame (optional)"
        frames={allFrames.filter((frame) => frame.key !== start?.key)}
        chosenKey={end?.key ?? null}
        allowNone
        onChoose={setEndKey}
      />

      {form && options.data && (
        <VideoOptionsFields
          form={form}
          options={options.data}
          disabled={queueing}
          startSize={startSize?.url === start?.url ? startSize?.size : undefined}
          onChange={setForm}
        />
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
      {viewed && <ImageViewer url={viewed.url} caption={viewed.caption} onClose={closeViewer} />}
    </form>
  )
}

interface FramePreviewProps {
  frame: VideoFrame | undefined
  caption: string
  emptyText: string
  onOpen: (viewed: ViewedFrame) => void
  onSize?: (size: Size) => void
}

function FramePreview({ frame, caption, emptyText, onOpen, onSize }: FramePreviewProps) {
  return (
    <figure>
      {frame ? (
        <button
          type="button"
          className="video-frame-open"
          title="Show at full resolution"
          onClick={() => onOpen({ url: frame.url, caption: `${caption} frame: ${frame.label}` })}
        >
          <img
            src={frame.url}
            alt={`${caption} frame`}
            onLoad={(e) => onSize?.({ width: e.currentTarget.naturalWidth, height: e.currentTarget.naturalHeight })}
          />
        </button>
      ) : (
        <div className="video-frame-empty">{emptyText}</div>
      )}
      <figcaption>{frame ? `${caption}: ${frame.label}` : caption}</figcaption>
    </figure>
  )
}

interface FrameChooserProps {
  label: string
  frames: VideoFrame[]
  chosenKey: string | null
  allowNone?: boolean
  onChoose: (key: string | null) => void
}

/** Collapsed at first: the chosen frame already shows in the previews above. */
function FrameChooser({ label, frames, chosenKey, allowNone = false, onChoose }: FrameChooserProps) {
  return (
    <details className="frame-chooser">
      <summary>Choose the {label.toLowerCase()}</summary>
      <div className="video-frame-choices" role="radiogroup" aria-label={label}>
        {allowNone && (
          <button
            type="button"
            role="radio"
            aria-checked={chosenKey === null}
            className={chosenKey === null ? 'chosen' : undefined}
            onClick={() => onChoose(null)}
          >
            None
          </button>
        )}
        {frames.map((frame) => (
          <button
            key={frame.key}
            type="button"
            role="radio"
            aria-checked={chosenKey === frame.key}
            aria-label={frame.label}
            title={frame.label}
            className={chosenKey === frame.key ? 'chosen' : undefined}
            onClick={() => onChoose(frame.key)}
          >
            <img src={frame.url} alt="" loading="lazy" />
          </button>
        ))}
      </div>
    </details>
  )
}
