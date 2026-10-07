import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { imageFileFrom, loadImageFile, sourceDimensions, sourceImageUrl, type ImageSource } from './imageSource'

type Tab = 'None' | 'Upload' | 'Paste' | 'DeviantArt'

const TABS: { tab: Tab; label: string }[] = [
  { tab: 'None', label: 'No image' },
  { tab: 'Upload', label: 'Upload' },
  { tab: 'Paste', label: 'Clipboard' },
  { tab: 'DeviantArt', label: 'DeviantArt' },
]

interface Props {
  source: ImageSource
  onChange: (source: ImageSource) => void
}

interface DeviationError {
  message: string
  existingSetId: number | null
}

export function ImageSourcePicker({ source, onChange }: Props) {
  const [tab, setTab] = useState<Tab>(source.kind === 'BasedOn' ? 'None' : source.kind)
  const [deviationUrl, setDeviationUrl] = useState('')
  const [deviationError, setDeviationError] = useState<DeviationError>()
  const [loadingDeviation, setLoadingDeviation] = useState(false)
  const [fileError, setFileError] = useState<string>()
  const [dragging, setDragging] = useState(false)

  const activeTab: Tab = source.kind === 'None' || source.kind === 'BasedOn' ? tab : source.kind

  async function attachFile(kind: 'Upload' | 'Paste', file: File | null) {
    if (!file) return
    try {
      setFileError(undefined)
      onChange(await loadImageFile(kind, file))
    } catch (error) {
      setFileError((error as Error).message)
    }
  }

  async function fetchDeviation(url: string) {
    const trimmed = url.trim()
    if (!trimmed || loadingDeviation) return
    setLoadingDeviation(true)
    setDeviationError(undefined)
    try {
      onChange({ kind: 'DeviantArt', preview: await api.previewDeviation(trimmed) })
    } catch (error) {
      const existingSetId = error instanceof ApiError ? error.existingSetId : null
      setDeviationError({ message: (error as Error).message, existingSetId })
      onChange({ kind: 'None' })
    } finally {
      setLoadingDeviation(false)
    }
  }

  function selectTab(next: Tab) {
    setTab(next)
    setFileError(undefined)
    setDeviationError(undefined)
    if (source.kind !== next) onChange({ kind: 'None' })
  }

  if (source.kind === 'BasedOn') {
    return (
      <div className="source-picker">
        <p className="hint">Uses the base image of set #{source.setId}.</p>
        {source.imageUrl && <img className="source-preview" src={source.imageUrl} alt="Base image" />}
      </div>
    )
  }

  const previewUrl = sourceImageUrl(source)
  const dimensions = sourceDimensions(source)

  return (
    <div className="source-picker">
      <div className="tabs" role="tablist">
        {TABS.map(({ tab: t, label }) => (
          <button
            key={t}
            type="button"
            role="tab"
            aria-selected={activeTab === t}
            className={activeTab === t ? 'tab active' : 'tab'}
            onClick={() => selectTab(t)}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'Upload' && (
        <label
          className={dragging ? 'drop-zone dragging' : 'drop-zone'}
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => {
            e.preventDefault()
            setDragging(false)
            void attachFile('Upload', imageFileFrom(e.dataTransfer))
          }}
        >
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp,image/gif"
            onChange={(e) => void attachFile('Upload', e.target.files?.[0] ?? null)}
          />
          <span>Drop an image here or click to choose a file</span>
        </label>
      )}

      {activeTab === 'Paste' && (
        <p className="drop-zone">Press Ctrl+V anywhere on this page to paste an image from the clipboard.</p>
      )}

      {activeTab === 'DeviantArt' && (
        <div className="deviation-input">
          <input
            type="url"
            placeholder="https://www.deviantart.com/artist/art/title-123456"
            value={deviationUrl}
            onChange={(e) => setDeviationUrl(e.target.value)}
            onPaste={(e) => {
              const pasted = e.clipboardData.getData('text')
              if (pasted) {
                e.preventDefault()
                setDeviationUrl(pasted)
                void fetchDeviation(pasted)
              }
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                void fetchDeviation(deviationUrl)
              }
            }}
          />
          <button type="button" onClick={() => void fetchDeviation(deviationUrl)} disabled={loadingDeviation}>
            {loadingDeviation ? 'Fetching…' : 'Fetch'}
          </button>
        </div>
      )}

      {fileError && <p className="error">{fileError}</p>}
      {deviationError && (
        <p className="error">
          {deviationError.message}{' '}
          {deviationError.existingSetId !== null && (
            <Link to={`/sets/${deviationError.existingSetId}`}>Open set #{deviationError.existingSetId}</Link>
          )}
        </p>
      )}

      {previewUrl && (
        <figure className="source-figure">
          <img className="source-preview" src={previewUrl} alt="Attached image" />
          <figcaption>
            {source.kind === 'DeviantArt' && (
              <>
                {source.preview.title ?? 'Untitled'} by {source.preview.author} ·{' '}
              </>
            )}
            {dimensions && `${dimensions.width}×${dimensions.height}`}
            <button type="button" className="link-button" onClick={() => onChange({ kind: 'None' })}>
              Remove
            </button>
          </figcaption>
        </figure>
      )}
    </div>
  )
}
