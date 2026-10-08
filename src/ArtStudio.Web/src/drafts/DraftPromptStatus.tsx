import type { PromptGenerationState, SetSummary } from '../api'

const BADGES: Partial<Record<PromptGenerationState, { label: string; status: string }>> = {
  Queued: { label: 'prompt waiting', status: 'Queued' },
  Running: { label: 'generating prompt…', status: 'Running' },
  Done: { label: 'prompt ready', status: 'Completed' },
  Failed: { label: 'prompt failed', status: 'Failed' },
}

export function DraftPromptStatus({ draft }: { draft: SetSummary }) {
  const { state, error, truncated } = draft.promptGeneration
  const badge = BADGES[state]
  if (!badge) return null

  return (
    <>
      <div className="job-meta">
        <span className={`badge status-${badge.status}`}>{badge.label}</span>
        {state === 'Done' && truncated && <span className="hint">may be cut off</span>}
      </div>
      {state === 'Failed' && error && <div className="job-error">{error}</div>}
    </>
  )
}
