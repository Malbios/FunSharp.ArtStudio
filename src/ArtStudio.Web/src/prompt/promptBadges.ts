import type { PromptGenerationKind, PromptGenerationState } from '../api'

type Badges = Partial<Record<PromptGenerationState, { label: string; status: string }>>

export const PROMPT_BADGES: Record<PromptGenerationKind, Badges> = {
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
