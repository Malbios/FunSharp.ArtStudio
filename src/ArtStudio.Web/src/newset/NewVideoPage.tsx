import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { useLoad } from '../live/useLoad'
import { MESSAGE_DURATION_MS } from '../messages'
import { closestResolution } from '../prompt/closestResolution'
import { initialVideoForm, resolveVideoForm, type VideoForm } from '../video/videoForm'
import { VideoOptionsFields, VideoPromptField } from '../video/VideoOptionsFields'
import { imageFileFrom, loadImageFile, NO_SOURCE, type ImageSource } from './imageSource'
import { ImageSourcePicker, type SourceTabOption } from './ImageSourcePicker'
import { NewModeTabs } from './NewModeTabs'

// Videos animate the start image directly, so only your own images qualify, not other artists' deviations.
const START_TABS: SourceTabOption[] = [
  { tab: 'Paste', label: 'Clipboard' },
  { tab: 'Upload', label: 'Upload' },
]
const END_TABS: SourceTabOption[] = [{ tab: 'None', label: 'No end frame' }, ...START_TABS]

type UploadedSource = Extract<ImageSource, { kind: 'Upload' | 'Paste' }>

function isUploaded(source: ImageSource): source is UploadedSource {
  return source.kind === 'Upload' || source.kind === 'Paste'
}

/** A new set that starts with videos of an uploaded or pasted image, optionally ending on a second one. */
export function NewVideoPage() {
  const options = useLoad(api.videoOptions)
  const resolutions = useLoad(api.resolutions)
  const [editedForm, setForm] = useState<VideoForm>()
  const form = editedForm ?? (options.data && initialVideoForm(options.data))
  const [start, setStart] = useState<ImageSource>(NO_SOURCE)
  const [end, setEnd] = useState<ImageSource>(NO_SOURCE)
  const [pickerVersion, setPickerVersion] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string>()
  const [createdId, setCreatedId] = useState<number>()

  useEffect(() => {
    if (createdId === undefined) return
    const timer = setTimeout(() => setCreatedId(undefined), MESSAGE_DURATION_MS)
    return () => clearTimeout(timer)
  }, [createdId])

  // A pasted image becomes the start frame, or the end frame once there is a start frame.
  useEffect(() => {
    function pasteImage(event: globalThis.ClipboardEvent) {
      const file = imageFileFrom(event.clipboardData)
      if (!file) return
      event.preventDefault()
      loadImageFile('Paste', file).then(
        (pasted) => (isUploaded(start) ? replace(end, setEnd, pasted) : replace(start, setStart, pasted)),
        (failure: Error) => setError(failure.message),
      )
    }
    window.addEventListener('paste', pasteImage)
    return () => window.removeEventListener('paste', pasteImage)
  })

  function replace(current: ImageSource, set: (source: ImageSource) => void, next: ImageSource) {
    if (isUploaded(current)) URL.revokeObjectURL(current.previewUrl)
    set(next)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!form || !options.data) return
    if (!isUploaded(start)) return setError('Upload or paste a start image.')
    const resolved = resolveVideoForm(form, options.data)
    if (resolved.error !== undefined) return setError(resolved.error)

    const settings = resolved.settings
    const data = new FormData()
    data.append('mode', 'video')
    data.append('prompt', settings.prompt)
    // The set's image resolution, for images made from it later; the video's own size is set by its megapixels.
    data.append('resolution', resolutions.data ? closestResolution(resolutions.data, start.width, start.height).name : 'Native')
    data.append('count', String(settings.count))
    data.append('seconds', String(settings.seconds))
    data.append('megapixels', String(settings.megapixels))
    data.append('turbo', String(settings.turbo))
    data.append('queueFirst', String(settings.queueFirst))
    data.append('sourceKind', start.kind)
    data.append('image', start.file)
    if (isUploaded(end)) data.append('endImage', end.file)

    setError(undefined)
    setCreatedId(undefined)
    setSubmitting(true)
    try {
      const { id } = await api.createSet(data)
      replace(start, setStart, NO_SOURCE)
      replace(end, setEnd, NO_SOURCE)
      setPickerVersion((version) => version + 1)
      setForm(undefined)
      setCreatedId(id)
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="new-set-layout">
      <form className="panel new-set" onSubmit={(e) => void submit(e)}>
        <div className="new-set-heading">
          <h2>New video</h2>
          <NewModeTabs video />
        </div>

        <div className="video-start-end">
          <div className="field">
            <span>Start image</span>
            <ImageSourcePicker
              key={`start-${pickerVersion}`}
              source={start}
              initialTab="Paste"
              tabs={START_TABS}
              onChange={(next) => replace(start, setStart, next)}
            />
          </div>
          <div className="field">
            <span>End image (optional)</span>
            <ImageSourcePicker
              key={`end-${pickerVersion}`}
              source={end}
              initialTab="None"
              tabs={END_TABS}
              onChange={(next) => replace(end, setEnd, next)}
            />
          </div>
        </div>

        {options.error && <p className="error">{options.error}</p>}
        {form && options.data && (
          <>
            <VideoOptionsFields
              form={form}
              options={options.data}
              disabled={submitting}
              startSize={isUploaded(start) ? start : undefined}
              onChange={setForm}
            />
            <VideoPromptField form={form} options={options.data} disabled={submitting} onChange={setForm} />
          </>
        )}

        <div className="field-row submit-row">
          {form && (
            <label className="submit-in-row inline-check" title="Put it ahead of the waiting videos; waiting images still run first">
              <input
                type="checkbox"
                checked={form.queueFirst}
                onChange={(e) => setForm({ ...form, queueFirst: e.target.checked })}
              />
              Queue first among the videos
            </label>
          )}
          <button type="submit" className="primary submit-in-row" disabled={submitting || !form}>
            {submitting ? 'Sending…' : 'Add to queue'}
          </button>
        </div>

        {error && <p className="error">{error}</p>}
        {createdId !== undefined && (
          <p className="success">
            Queued as <Link to={`/sets/${createdId}`}>set #{createdId}</Link>. <Link to="/queue">View queue</Link>
          </p>
        )}
      </form>
    </div>
  )
}
