import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type SyntheticEvent } from 'react'
import { createPortal } from 'react-dom'
import { api, type DeviationPreview } from '../api'

const MAX_CONTEXT_LENGTH = 1000

interface Props {
  url: string
  onConfirm: (context: string) => void
  onCancel: () => void
}

/** Asks what the vision model should know about one DeviantArt link's image before it becomes a draft. */
export function ContextDialog({ url, onConfirm, onCancel }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [context, setContext] = useState('')
  const [preview, setPreview] = useState<DeviationPreview>()
  const [previewError, setPreviewError] = useState<string>()

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  useEffect(() => {
    let current = true
    api.previewDeviation(url).then(
      (loaded) => current && setPreview(loaded),
      (failure: Error) => current && setPreviewError(failure.message),
    )
    return () => {
      current = false
    }
  }, [url])

  function confirm(event?: FormEvent) {
    event?.preventDefault()
    onConfirm(context.trim())
  }

  function cancel(event: SyntheticEvent) {
    event.preventDefault()
    onCancel()
  }

  function confirmOnCtrlEnter(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) confirm(event)
  }

  const image = preview?.images[0]

  return createPortal(
    <dialog ref={dialogRef} className="modify-dialog context-dialog" onCancel={cancel}>
      <form onSubmit={confirm}>
        <h3>Additional context</h3>
        <div className="context-dialog-body">
          <div className="context-dialog-image">
            {image && <img src={image.imageUrl} alt={preview.title ?? 'DeviantArt image'} />}
            {preview && (
              <p className="hint">
                {preview.title ?? 'Untitled'} by {preview.author}
              </p>
            )}
            {previewError && <p className="error">{previewError}</p>}
            {!preview && !previewError && <p className="hint">Loading…</p>}
          </div>
          <label className="field">
            <span className="context-dialog-url">{url}</span>
            <textarea
              autoFocus
              rows={8}
              value={context}
              maxLength={MAX_CONTEXT_LENGTH}
              placeholder="What the vision model should know about this image, e.g. who a person is"
              onChange={(e) => setContext(e.target.value)}
              onKeyDown={confirmOnCtrlEnter}
            />
          </label>
        </div>
        <div className="modify-dialog-actions">
          <button type="button" onClick={cancel}>
            Cancel
          </button>
          <button type="submit" className="primary" title="Ctrl+Enter">
            OK
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  )
}
