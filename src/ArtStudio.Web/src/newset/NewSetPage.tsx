import { useEffect, useState, type ClipboardEvent, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api, type SetDetail } from '../api'
import { useLoad } from '../live/useLoad'
import { MESSAGE_DURATION_MS } from '../messages'
import { cleanPrompt } from '../prompt/cleanPrompt'
import { closestResolution } from '../prompt/closestResolution'
import { insertAtSelection } from '../prompt/insertAtSelection'
import { BuildingBlockChips } from './BuildingBlockChips'
import { imageFileFrom, loadImageFile, NO_SOURCE, sourceDimensions, type ImageSource } from './imageSource'
import { ImageSourcePicker, type SourceTab } from './ImageSourcePicker'

const DEFAULT_COUNT = 2

interface CreatedSet {
  id: number
  asDraft: boolean
}

export function NewSetPage() {
  const [searchParams] = useSearchParams()
  const draftId = Number(searchParams.get('draft')) || null
  const baseSetId = draftId ?? (Number(searchParams.get('basedOn')) || null)
  const isDraft = draftId !== null
  const navigate = useNavigate()
  const resolutions = useLoad(api.resolutions)

  const [prompt, setPrompt] = useState('')
  const [source, setSource] = useState<ImageSource>(NO_SOURCE)
  const [resolution, setResolution] = useState('')
  const [resolutionIsAuto, setResolutionIsAuto] = useState(false)
  const [count, setCount] = useState(DEFAULT_COUNT)
  const [pickerVersion, setPickerVersion] = useState(0)
  const [pickerTab, setPickerTab] = useState<SourceTab>('DeviantArt')
  const [baseSet, setBaseSet] = useState<SetDetail>()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string>()
  const [created, setCreated] = useState<CreatedSet>()

  useEffect(() => {
    if (created === undefined) return
    const timer = setTimeout(() => setCreated(undefined), MESSAGE_DURATION_MS)
    return () => clearTimeout(timer)
  }, [created])

  useEffect(() => {
    if (baseSetId === null) return
    api.set(baseSetId).then((set) => {
      setBaseSet(set)
      setPrompt(set.prompt)
      setResolution(set.resolution)
      setResolutionIsAuto(false)
      setSource({ kind: 'BasedOn', imageUrl: set.sourceImageUrl, deviantArtAuthor: set.deviantArtAuthor })
    }, (failure: Error) => setError(failure.message))
  }, [baseSetId])

  useEffect(() => {
    function pasteImage(event: globalThis.ClipboardEvent) {
      const file = imageFileFrom(event.clipboardData)
      if (!file || baseSetId !== null) return
      event.preventDefault()
      loadImageFile('Paste', file).then(changeSource, (failure: Error) => setError(failure.message))
    }
    window.addEventListener('paste', pasteImage)
    return () => window.removeEventListener('paste', pasteImage)
  })

  function changeSource(next: ImageSource) {
    if (source.kind === 'Upload' || source.kind === 'Paste') URL.revokeObjectURL(source.previewUrl)
    setSource(next)

    const dimensions = sourceDimensions(next)
    if (dimensions && resolutions.data) {
      setResolution(closestResolution(resolutions.data, dimensions.width, dimensions.height).name)
      setResolutionIsAuto(true)
    } else if (resolutionIsAuto) {
      setResolution('')
      setResolutionIsAuto(false)
    }
  }

  function pastePromptText(event: ClipboardEvent<HTMLTextAreaElement>) {
    if (imageFileFrom(event.clipboardData)) return
    const text = event.clipboardData.getData('text')
    if (!text) return
    event.preventDefault()
    insertAtSelection(event.currentTarget, cleanPrompt(text))
  }

  function resetForm() {
    setPickerTab(source.kind === 'BasedOn' ? 'None' : source.kind)
    setPickerVersion((version) => version + 1)
    changeSource(NO_SOURCE)
    setPrompt('')
    setResolution('')
    setResolutionIsAuto(false)
    setCount(DEFAULT_COUNT)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!prompt.trim()) return setError('Enter a prompt.')
    await send(async () => {
      if (baseSet && isDraft) {
        await api.queueDraft(baseSet.id, prompt, resolution, count)
        navigate('/drafts')
      } else if (baseSet) {
        await api.moreImages(baseSet.id, count, prompt, resolution)
        navigate(`/sets/${baseSet.id}`)
      } else {
        const { id } = await api.createSet(buildForm())
        resetForm()
        setCreated({ id, asDraft: false })
      }
    })
  }

  async function saveDraft() {
    await send(async () => {
      const form = buildForm()
      form.append('draft', 'true')
      const { id } = await api.createSet(form)
      resetForm()
      setCreated({ id, asDraft: true })
    })
  }

  async function send(action: () => Promise<void>) {
    setError(undefined)
    setCreated(undefined)
    if (!resolution) return setError('Choose a resolution.')

    setSubmitting(true)
    try {
      await action()
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  function buildForm() {
    const form = new FormData()
    form.append('prompt', prompt)
    form.append('resolution', resolution)
    form.append('count', String(count))
    switch (source.kind) {
      case 'Upload':
      case 'Paste':
        form.append('sourceKind', source.kind)
        form.append('image', source.file)
        break
      case 'DeviantArt':
        form.append('sourceKind', 'DeviantArt')
        form.append('deviantArtUrl', source.preview.url)
        form.append('deviantArtImageIndex', String(source.imageIndex))
        break

    }
    return form
  }

  const hasImage = source.kind === 'Upload' || source.kind === 'Paste' || source.kind === 'DeviantArt'
  const heading = !baseSet ? 'New prompt' : isDraft ? `Queue draft #${baseSet.id}` : `Edit & requeue set #${baseSet.id}`

  const promptField = (
    <label className="field">
      <span>Prompt</span>
      <textarea
        value={prompt}
        rows={8}
        onChange={(e) => setPrompt(e.target.value)}
        onPaste={pastePromptText}
        placeholder="Describe the image…"
      />
    </label>
  )

  const imageField = (
    <div className="field">
      <span>{baseSet ? 'Inspiration image' : 'Image (optional)'}</span>
      <ImageSourcePicker key={pickerVersion} source={source} initialTab={pickerTab} onChange={changeSource} />
    </div>
  )

  return (
    <div className="new-set-layout">
      <form className="panel new-set" onSubmit={(e) => void submit(e)}>
        <h2>{heading}</h2>

        <div className="field-row submit-row">
          <label className="field">
            <span>
              Resolution {resolutionIsAuto && <em className="badge">auto from image</em>}
            </span>
            <select
              value={resolution}
              onChange={(e) => {
                setResolution(e.target.value)
                setResolutionIsAuto(false)
              }}
            >
              <option value="" disabled>
                Choose…
              </option>
              {resolutions.data?.map((preset) => (
                <option key={preset.name} value={preset.name}>
                  {preset.name} ({preset.width}×{preset.height})
                </option>
              ))}
            </select>
          </label>

          <label className="field narrow">
            <span>Images</span>
            <input
              type="number"
              min={1}
              max={100}
              value={count}
              onChange={(e) => setCount(Math.max(1, Number(e.target.value) || 1))}
            />
          </label>
          {baseSetId === null && (
            <button
              type="button"
              className="submit-in-row"
              disabled={submitting || !hasImage}
              title={hasImage ? 'Keep the image without a prompt and queue it later' : 'Attach an image first'}
              onClick={() => void saveDraft()}
            >
              Save as draft
            </button>
          )}
          <button type="submit" className="primary submit-in-row" disabled={submitting}>
            {submitting ? 'Sending…' : 'Add to queue'}
          </button>
        </div>

        {error && <p className="error">{error}</p>}
        {created && !created.asDraft && (
          <p className="success">
            Queued as <Link to={`/sets/${created.id}`}>set #{created.id}</Link>. <Link to="/queue">View queue</Link>
          </p>
        )}
        {created?.asDraft && (
          <p className="success">
            Saved as draft #{created.id}. <Link to="/drafts">View drafts</Link>
          </p>
        )}

        {baseSet ? (
          <div className="image-beside-prompt">
            {imageField}
            {promptField}
          </div>
        ) : (
          <>
            {promptField}
            {imageField}
          </>
        )}
      </form>

      <BuildingBlockChips />
    </div>
  )
}
