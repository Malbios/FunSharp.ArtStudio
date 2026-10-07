import { useEffect } from 'react'
import type { ImageInfo } from '../api'

interface Props {
  images: ImageInfo[]
  index: number
  sourceImageUrl: string | null
  selectedImageId: number | null
  onIndexChange: (index: number) => void
  onToggleSelect: (image: ImageInfo) => void
  onClose: () => void
}

export function Lightbox({ images, index, sourceImageUrl, selectedImageId, onIndexChange, onToggleSelect, onClose }: Props) {
  const image = images[index]
  const isSelected = image.id === selectedImageId

  useEffect(() => {
    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
      if (event.key === 'ArrowLeft' && index > 0) onIndexChange(index - 1)
      if (event.key === 'ArrowRight' && index < images.length - 1) onIndexChange(index + 1)
    }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [index, images.length, onClose, onIndexChange])

  return (
    <div className="lightbox" role="dialog" aria-modal>
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
        <button type="button" className={isSelected ? '' : 'primary'} onClick={() => onToggleSelect(image)}>
          {isSelected ? 'Unpick' : 'Pick this image'}
        </button>
        <a href={image.url} target="_blank" rel="noreferrer">
          Open original
        </a>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
      <div className="lightbox-images">
        {sourceImageUrl && (
          <figure>
            <img src={sourceImageUrl} alt="Base image" />
            <figcaption>Base image</figcaption>
          </figure>
        )}
        <figure>
          <img src={image.url} alt={`Generated image ${index + 1}`} />
          <figcaption>{isSelected ? 'Generated (picked)' : 'Generated'}</figcaption>
        </figure>
      </div>
    </div>
  )
}
