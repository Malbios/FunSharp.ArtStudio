import { useState } from 'react'
import { api, type PromptGenerationKind, type PromptGenerationState, type SetSummary } from '../api'
import { generateActionLabel } from '../prompt/generateAction'

type Badges = Partial<Record<PromptGenerationState, { label: string; status: string }>>

const BADGES: Record<PromptGenerationKind, Badges> = {
  Generate: {
    Queued: { label: 'prompt waiting', status: 'Queued' },
    Running: { label: 'generating prompt…', status: 'Running' },
    Done: { label: 'prompt ready', status: 'Completed' },
    Failed: { label: 'prompt failed', status: 'Failed' },
  },
  Modify: {
    Queued: { label: 'modification waiting', status: 'Queued' },
    Running: { label: 'modifying prompt…', status: 'Running' },
    Done: { label: 'prompt modified', status: 'Completed' },
    Failed: { label: 'modifying failed', status: 'Failed' },
  },
}

export function DraftPromptStatus({ draft, onError }: { draft: SetSummary; onError: (message: string) => void }) {
  const [busy, setBusy] = useState(false)
  const { state, kind, error, truncated } = draft.promptGeneration
  const badge = BADGES[kind][state]
  const action = generateActionLabel(draft.promptGeneration, draft.prompt.trim() !== '')

  async function generate() {
    setBusy(true)
    try {
      await api.generatePrompt(draft.id)
    } catch (failure) {
      onError((failure as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <div className="job-meta">
        {badge && <span className={`badge status-${badge.status}`}>{badge.label}</span>}
        {state === 'Done' && truncated && <span className="hint">may be cut off</span>}
        {action && (
          <button type="button" className="link-button" disabled={busy} onClick={() => void generate()}>
            {action}
          </button>
        )}
      </div>
      {state === 'Failed' && error && <div className="job-error">{error}</div>}
    </>
  )
}
