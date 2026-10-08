import { useEffect, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { api } from '../api'

interface Props {
  /** What is being modified, shown as the heading: "Whole prompt" or a box label. */
  subject: string
  text: string
  /** The box label sent to the model; undefined modifies the whole prompt. */
  section?: string
  onModified: (text: string, truncated: boolean) => void
  onClose: () => void
}

export function ModifyPromptDialog({ subject, text, section, onModified, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const abortRef = useRef<AbortController>(undefined)
  const [instructions, setInstructions] = useState('')
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    dialogRef.current?.showModal()
    return () => abortRef.current?.abort()
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    // The dialog is portalled out of the page's form, but React still bubbles its submit to that form.
    event.stopPropagation()
    if (!instructions.trim()) return setError('Describe how it should change.')

    const controller = new AbortController()
    abortRef.current = controller
    setError(undefined)
    setRunning(true)
    try {
      const result = await api.modifyPrompt(text, instructions, section, controller.signal)
      onModified(result.text, result.truncated)
      onClose()
    } catch (failure) {
      if (controller.signal.aborted) return
      setError((failure as Error).message)
      setRunning(false)
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
            disabled={running}
            placeholder="For example: make it night-time"
            onChange={(e) => setInstructions(e.target.value)}
          />
        </label>
        {error && <p className="error">{error}</p>}
        <div className="modify-dialog-actions">
          <button type="button" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="primary" disabled={running}>
            {running ? 'Modifying…' : 'Modify'}
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  )
}
