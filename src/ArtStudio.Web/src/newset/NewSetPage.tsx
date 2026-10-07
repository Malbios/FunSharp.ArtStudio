import { useEffect, useState, type ClipboardEvent, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { api, type SetDetail } from '../api'
import { useLoad } from '../live/useLoad'
import { cleanPrompt } from '../prompt/cleanPrompt'
import { closestResolution } from '../prompt/closestResolution'
import { BuildingBlockChips } from './BuildingBlockChips'
import { imageFileFrom, loadImageFile, NO_SOURCE, sourceDimensions, type ImageSource } from './imageSource'
import { ImageSourcePicker, type SourceTab } from './ImageSourcePicker'

const DEFAULT_COUNT = 2

export function NewSetPage() {
  const [searchParams] = useSearchParams()
  const basedOnId = Number(searchParams.get('basedOn')) || null
  const navigate = useNavigate()
  const resolutions = useLoad(api.resolutions)

  const [prompt, setPrompt] = useState('')
  const [source, setSource] = useState<ImageSource>(NO_SOURCE)
  const [resolution, setResolution] = useState('')
  const [resolutionIsAuto, setResolutionIsAuto] = useState(false)
  const [count, setCount] = useState(DEFAULT_COUNT)
  const [pickerVersion, setPickerVersion] = useState(0)
  const [pickerTab, setPickerTab] = useState<SourceTab>('None')
  const [baseSet, setBaseSet] = useState<SetDetail>()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string>()
  const [createdSetId, setCreatedSetId] = useState<number>()

  useEffect(() => {
    if (basedOnId === null) return
    api.set(basedOnId).then((set) => {
      setBaseSet(set)
      setPrompt(set.prompt)
      setResolution(set.resolution)
      setResolutionIsAuto(false)
      setSource({ kind: 'BasedOn', setId: set.id, imageUrl: set.sourceImageUrl })
    }, (failure: Error) => setError(failure.message))
  }, [basedOnId])

  useEffect(() => {
    function pasteImage(event: globalThis.ClipboardEvent) {
      const file = imageFileFrom(event.clipboardData)
      if (!file || basedOnId !== null) return
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
    const textarea = event.currentTarget
    const cleaned = cleanPrompt(text)
    const caret = textarea.selectionStart + cleaned.length
    setPrompt(prompt.slice(0, textarea.selectionStart) + cleaned + prompt.slice(textarea.selectionEnd))
    requestAnimationFrame(() => textarea.setSelectionRange(caret, caret))
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
    setError(undefined)
    setCreatedSetId(undefined)
    if (!prompt.trim()) return setError('Enter a prompt.')
    if (!resolution) return setError('Choose a resolution.')

    setSubmitting(true)
    try {
      if (baseSet) {
        await api.moreImages(baseSet.id, count, prompt, resolution)
        navigate(`/sets/${baseSet.id}`)
        return
      }

      const { id } = await api.createSet(buildForm())
      resetForm()
      setCreatedSetId(id)
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
        break

    }
    return form
  }

  return (
    <div className="new-set-layout">
      <form className="panel new-set" onSubmit={(e) => void submit(e)}>
        <h2>{baseSet ? `Edit & requeue set #${baseSet.id}` : 'New prompt'}</h2>
        {baseSet && (
          <p className="hint">New images are added to set #{baseSet.id} with this prompt and resolution.</p>
        )}

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

        <div className="field">
          <span>Image (optional)</span>
          <ImageSourcePicker key={pickerVersion} source={source} initialTab={pickerTab} onChange={changeSource} />
        </div>

        <div className="field-row">
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
        </div>

        {error && <p className="error">{error}</p>}
        {createdSetId !== undefined && (
          <p className="success">
            Queued as <Link to={`/sets/${createdSetId}`}>set #{createdSetId}</Link>. <Link to="/queue">View queue</Link>
          </p>
        )}

        <button type="submit" className="primary" disabled={submitting}>
          {submitting ? 'Queuing…' : 'Add to queue'}
        </button>
      </form>

      <BuildingBlockChips />
    </div>
  )
}
