import type { PromptGenerationState } from '../api'

/** The label of the generate button, or undefined while a prompt is waiting or being generated. */
export function generateActionLabel(state: PromptGenerationState, hasPrompt: boolean): string | undefined {
  if (state === 'Queued' || state === 'Running') return undefined
  if (state === 'Failed') return 'Retry generating'
  return hasPrompt ? 'Generate new prompt' : 'Generate prompt'
}
