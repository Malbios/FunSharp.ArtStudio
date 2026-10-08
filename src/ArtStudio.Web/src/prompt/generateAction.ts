import type { PromptGeneration } from '../api'

/** The label of the generate button, or undefined while a prompt is waiting, being generated or being modified. */
export function generateActionLabel(
  { state, kind }: Pick<PromptGeneration, 'state' | 'kind'>,
  hasPrompt: boolean,
): string | undefined {
  if (state === 'Queued' || state === 'Running') return undefined
  if (state === 'Failed' && kind === 'Generate') return 'Retry generating'
  return hasPrompt ? 'Generate new prompt' : 'Generate prompt'
}

export function isPromptPending({ state }: Pick<PromptGeneration, 'state'>): boolean {
  return state === 'Queued' || state === 'Running'
}
