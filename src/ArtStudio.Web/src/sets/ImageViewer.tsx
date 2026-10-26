import { useEffect } from 'react'

interface Props {
  url: string
  caption: string
  onClose: () => void
}

/** Shows one image at full resolution over the page; a click anywhere or Escape closes it. */
export function ImageViewer({ url, caption, onClose }: Props) {
  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  return (
    <div className="lightbox" role="dialog" aria-modal aria-label={caption} onClick={onClose}>
      <div className="lightbox-bar">
        <span>{caption}</span>
        <span className="spacer" />
        <a href={url} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>
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
    </div>
  )
}
