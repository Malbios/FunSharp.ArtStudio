import { useEffect, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'

interface Props {
  /** What is being modified, shown as the heading: "Whole prompt" or a box label. */
  subject: string
  text: string
  hint?: string
  /** Queues the modification; a rejection is shown in the dialog. */
  onSubmit: (instructions: string) => Promise<void>
  onClose: () => void
}

export function ModifyPromptDialog({ subject, text, hint, onSubmit, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [instructions, setInstructions] = useState('')
  const [queueing, setQueueing] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    dialogRef.current?.showModal()
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    // The dialog is portalled out of the page's form, but React still bubbles its submit to that form.
    event.stopPropagation()
    if (!instructions.trim()) return setError('Describe how it should change.')

    setError(undefined)
    setQueueing(true)
    try {
      await onSubmit(instructions)
      onClose()
    } catch (failure) {
      setError((failure as Error).message)
      setQueueing(false)
    }
  }

  return createPortal(
    <dialog ref={dialogRef} className="modify-dialog" onClose={onClose}>
      <form onSubmit={(e) => void submit(e)}>
        <h3>Modify: {subject}</h3>
        <p className="modify-dialog-text">{text}</p>
        <label className="field">
          <span>Instructions</span>
          <textarea
            autoFocus
            rows={4}
            value={instructions}
            disabled={queueing}
            placeholder="For example: make it night-time"
            onChange={(e) => setInstructions(e.target.value)}
          />
        </label>
        {hint && <p className="hint">{hint}</p>}
        {error && <p className="error">{error}</p>}
        <div className="modify-dialog-actions">
          <button type="button" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="primary" disabled={queueing}>
            {queueing ? 'Queueing…' : 'Modify'}
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  )
}
