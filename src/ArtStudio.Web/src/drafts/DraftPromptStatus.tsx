import { useState } from 'react'
import { api, type PromptGenerationState, type SetSummary } from '../api'

const BADGES: Partial<Record<PromptGenerationState, { label: string; status: string }>> = {
  Queued: { label: 'prompt waiting', status: 'Queued' },
  Running: { label: 'generating prompt…', status: 'Running' },
  Done: { label: 'prompt ready', status: 'Completed' },
  Failed: { label: 'prompt failed', status: 'Failed' },
}

const ACTIONS: Partial<Record<PromptGenerationState, string>> = {
  None: 'Generate prompt',
  Done: 'Regenerate',
  Failed: 'Retry',
}

export function DraftPromptStatus({ draft, onError }: { draft: SetSummary; onError: (message: string) => void }) {
  const [busy, setBusy] = useState(false)
  const { state, error, truncated } = draft.promptGeneration
  const badge = BADGES[state]
  const action = ACTIONS[state]

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
