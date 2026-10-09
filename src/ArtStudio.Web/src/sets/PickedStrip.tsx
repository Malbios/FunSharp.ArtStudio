import type { ImageInfo } from '../api'
import { movedItem, useDragReorder } from './useDragReorder'

interface Props {
  pickedImages: ImageInfo[]
  onReorder: (imageIds: number[]) => void
  onUnpick: (image: ImageInfo) => void
  onOpen: (image: ImageInfo) => void
}

export function PickedStrip({ pickedImages, onReorder, onUnpick, onOpen }: Props) {
  const ids = pickedImages.map((image) => image.id)

  function move(from: number, to: number) {
    if (to < 0 || to >= ids.length || from === to) return
    onReorder(movedItem(ids, from, to))
  }

  const drag = useDragReorder(move)

  return (
    <section className="panel picked-strip">
      <h3>Picked for DeviantArt</h3>
      {pickedImages.length === 0 ? (
        <p className="hint">Nothing picked yet. Pick images with the ☆ on a tile or in the image view.</p>
      ) : (
        <>
          <p className="hint">Drag to reorder, or use the arrows. #1 is the main image of the post.</p>
          <ol className="picked-list">
            {pickedImages.map((image, index) => (
              <li key={image.id} className={`picked-item ${drag.itemClass(index)}`} {...drag.itemProps(index)}>
                <button type="button" className="picked-thumb" onClick={() => onOpen(image)}>
                  <img src={image.url} alt={`Picked image ${index + 1}`} draggable={false} />
                  <span className="picked-number">{index === 0 ? '#1 ★ main' : `#${index + 1}`}</span>
                </button>
                <div className="picked-controls">
                  <button type="button" disabled={index === 0} onClick={() => move(index, index - 1)} title="Move earlier">
                    ←
                  </button>
                  <button
                    type="button"
                    disabled={index === pickedImages.length - 1}
                    onClick={() => move(index, index + 1)}
                    title="Move later"
                  >
                    →
                  </button>
                  <button type="button" className="danger" onClick={() => onUnpick(image)} title="Remove from picks">
                    ×
                  </button>
                </div>
              </li>
            ))}
          </ol>
        </>
      )}
    </section>
  )
}
