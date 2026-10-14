import { useEffect, type MouseEvent } from 'react'
import { createPortal } from 'react-dom'

interface Props {
  url: string
  caption: string
  onClose: () => void
}

/** One image shown full size over the page. */
export function ImageLightbox({ url, caption, onClose }: Props) {
  useEffect(() => {
    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onClose])

  function closeOnBackdropClick(event: MouseEvent) {
    if (!(event.target as HTMLElement).closest('img, button, a')) onClose()
  }

  return createPortal(
    <div className="lightbox" role="dialog" aria-modal onClick={closeOnBackdropClick}>
      <div className="lightbox-bar">
        <span>{caption}</span>
        <span className="spacer" />
        <a href={url} target="_blank" rel="noreferrer">
          Open original
        </a>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
      <div className="lightbox-body single">
        <div className="lightbox-images">
          <figure>
            <img src={url} alt={caption} />
          </figure>
        </div>
      </div>
    </div>,
    document.body,
  )
}
