import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type SyntheticEvent } from 'react'
import { createPortal } from 'react-dom'
import { api, type DeviationPreview } from '../api'

const MAX_CONTEXT_LENGTH = 1000

interface Props {
  url: string
  onConfirm: (context: string) => void
  onCancel: () => void
  /** The link can't become a draft, so the dialog never opens. */
  onRejected: (failure: Error) => void
}

/** Asks what the vision model should know about one DeviantArt link's image before it becomes a draft. */
export function ContextDialog({ url, onConfirm, onCancel, onRejected }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const onRejectedRef = useRef(onRejected)
  const [context, setContext] = useState('')
  const [preview, setPreview] = useState<DeviationPreview>()

  useEffect(() => {
    onRejectedRef.current = onRejected
  })

  useEffect(() => {
    let current = true
    api.previewDeviation(url).then(
      (loaded) => current && setPreview(loaded),
      (failure: Error) => current && onRejectedRef.current(failure),
    )
    return () => {
      current = false
    }
  }, [url])

  useEffect(() => {
    if (!preview) return
    dialogRef.current?.showModal()
    textareaRef.current?.focus()
  }, [preview])

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
          </div>
          <label className="field">
            <span className="context-dialog-url">{url}</span>
            <textarea
              ref={textareaRef}
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
