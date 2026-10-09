import { useEffect, useState, type MouseEvent } from 'react'
import { Link } from 'react-router-dom'
import type { ImageInfo } from '../api'
import { RequeueWithPreset } from './RequeueWithPreset'

interface Props {
  images: ImageInfo[]
  index: number
  sourceImageUrl: string | null
  pickedImageIds: number[]
  canRequeue: boolean
  /** Images queued by Requeue with preset when the preset has no image count. */
  requeueCount: number
  onRequeued: () => void
  onError: (message: string) => void
  onIndexChange: (index: number) => void
  onTogglePick: (image: ImageInfo) => void
  onClose: () => void
}

export function Lightbox({
  images,
  index,
  sourceImageUrl,
  pickedImageIds,
  canRequeue,
  requeueCount,
  onRequeued,
  onError,
  onIndexChange,
  onTogglePick,
  onClose,
}: Props) {
  const image = images[index]
  const pickNumber = pickedImageIds.indexOf(image.id) + 1
  const [copiedImageId, setCopiedImageId] = useState<number>()

  function closeOnBackdropClick(event: MouseEvent) {
    const clickedContent = (event.target as HTMLElement).closest('img, button, a, .lightbox-prompt')
    if (!clickedContent) onClose()
  }

  async function copyPrompt() {
    await navigator.clipboard.writeText(image.prompt)
    setCopiedImageId(image.id)
  }

  useEffect(() => {
    function handleKey(event: KeyboardEvent) {
      if (document.querySelector('dialog[open]')) return
      if (event.key === 'Escape') onClose()
      if (event.key === 'ArrowLeft' && index > 0) onIndexChange(index - 1)
      if (event.key === 'ArrowRight' && index < images.length - 1) onIndexChange(index + 1)
    }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [index, images.length, onClose, onIndexChange])

  const copyPromptButton = (
    <button type="button" className="link-button" onClick={() => void copyPrompt()}>
      {copiedImageId === image.id ? 'Copied!' : 'Copy prompt'}
    </button>
  )

  return (
    <div className="lightbox" role="dialog" aria-modal onClick={closeOnBackdropClick}>
      <div className="lightbox-bar">
        <button type="button" disabled={index === 0} onClick={() => onIndexChange(index - 1)}>
          ← Previous
        </button>
        <span>
          {index + 1} / {images.length} · seed {image.seed}
        </span>
        <button type="button" disabled={index === images.length - 1} onClick={() => onIndexChange(index + 1)}>
          Next →
        </button>
        <span className="spacer" />
        <button type="button" className={pickNumber > 0 ? '' : 'primary'} onClick={() => onTogglePick(image)}>
          {pickNumber > 0 ? `Unpick (#${pickNumber})` : 'Pick this image'}
        </button>
        <a href={image.url} target="_blank" rel="noreferrer">
          Open original
        </a>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
      <div className="lightbox-body">
        <div className="lightbox-images">
          <figure className="generated">
            <img src={image.url} alt={`Generated image ${index + 1}`} />
            <figcaption>{pickNumber > 0 ? `Generated (picked #${pickNumber})` : 'Generated'}</figcaption>
          </figure>
          {sourceImageUrl && (
            <figure>
              <img src={sourceImageUrl} alt="Base image" />
              <figcaption>Base image</figcaption>
            </figure>
          )}
        </div>
        <div className="lightbox-prompt">
          <div className="job-meta">
            {copyPromptButton}
            {canRequeue && (
              <Link to={`/?basedOn=${image.setId}&image=${image.id}`} className="link-button">
                Edit &amp; requeue with this prompt
              </Link>
            )}
            {canRequeue && (
              <RequeueWithPreset
                setId={image.setId}
                prompt={image.prompt}
                resolution={image.resolution}
                fallbackCount={requeueCount}
                className="link-button"
                onQueued={onRequeued}
                onError={onError}
              />
            )}
          </div>
          <p>{image.prompt}</p>
          <div className="job-meta">
            {copyPromptButton}
            <span>{image.resolution}</span>
            <span>seed {image.seed}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
