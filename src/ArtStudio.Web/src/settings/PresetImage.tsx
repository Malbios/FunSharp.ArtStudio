import { useCallback, useRef, useState } from 'react'
import type { BuildingBlock } from '../api'
import { ImageLightbox } from './ImageLightbox'

const ACCEPTED_TYPES = 'image/png,image/jpeg,image/webp,image/gif'

/** Reads the first image on the clipboard; text or a refused permission ends in a readable error. */
export async function readClipboardImage(): Promise<Blob> {
  let items: ClipboardItems
  try {
    items = await navigator.clipboard.read()
  } catch {
    throw new Error('The browser did not allow reading the clipboard.')
  }
  for (const item of items) {
    const type = item.types.find((candidate) => candidate.startsWith('image/'))
    if (type) return item.getType(type)
  }
  throw new Error('There is no image on the clipboard.')
}

interface Props {
  preset: BuildingBlock
  onImage: (image: Blob) => void
  onPasteFromClipboard: () => void
  onRemove: () => void
}

export function PresetImage({ preset, onImage, onPasteFromClipboard, onRemove }: Props) {
  const fileInput = useRef<HTMLInputElement>(null)
  const [viewing, setViewing] = useState(false)
  const closeViewer = useCallback(() => setViewing(false), [])

  return (
    <div className="preset-image">
      {preset.imageUrl ? (
        <button type="button" className="preset-image-open" title="View full size" onClick={() => setViewing(true)}>
          <img src={preset.imageUrl} alt={`Example for ${preset.label}`} />
        </button>
      ) : (
        <span className="preset-image-empty">No image</span>
      )}
      <button type="button" onClick={() => fileInput.current?.click()}>
        Upload…
      </button>
      <button type="button" onClick={onPasteFromClipboard} title="Or press Ctrl+V while editing this preset">
        Paste
      </button>
      {preset.imageUrl && (
        <button type="button" onClick={onRemove} title="Remove the example image">
          Remove
        </button>
      )}
      <input
        ref={fileInput}
        type="file"
        accept={ACCEPTED_TYPES}
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0]
          e.target.value = ''
          if (file) onImage(file)
        }}
      />
      {viewing && preset.imageUrl && (
        <ImageLightbox url={preset.imageUrl} caption={`Example for ${preset.label}`} onClose={closeViewer} />
      )}
    </div>
  )
}
