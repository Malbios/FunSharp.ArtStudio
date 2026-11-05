import type { PromptGeneration } from '../api'
import { formatDuration } from '../queue/jobLabels'

type RunTimes = Pick<PromptGeneration, 'state' | 'startedAt' | 'finishedAt'>

/** How long the latest prompt run has been running, or took; null when there is no such run. */
export function promptTimeLabel({ state, startedAt, finishedAt }: RunTimes, now: number): string | null {
  if (!startedAt) return null
  const started = Date.parse(startedAt)
  if (state === 'Running') return `running for ${formatDuration(Math.max(0, now - started) / 1000)}`
  if ((state === 'Done' || state === 'Failed') && finishedAt) return `took ${formatDuration((Date.parse(finishedAt) - started) / 1000)}`
  return null
}
