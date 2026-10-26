import type { Job } from '../api'

export function jobProgressLabel(job: Job): string {
  return `${job.completedCount}/${job.requestedCount} ${job.kind === 'Video' ? 'videos' : 'images'}`
}

/** The size, and for videos also the length and whether turbo is on. */
export function jobSettingsLabel(job: Job): string {
  if (job.kind !== 'Video') return job.resolution
  return [job.resolution, `${job.videoSeconds} s`, job.videoTurbo ? 'turbo' : 'full quality', job.hasEndFrame ? 'end frame' : '']
    .filter(Boolean)
    .join(' · ')
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return 'less than a minute'
  const minutes = Math.round(seconds / 60)
  if (minutes < 90) return `${minutes} min`
  return `${Math.floor(minutes / 60)} h ${minutes % 60} min`
}

/** How long a video job is expected to take or still take; null when there is nothing to estimate. */
export function jobTimeLabel(job: Job, now: number): string | null {
  const perVideo = job.estimatedSecondsPerVideo
  const remaining = job.requestedCount - job.completedCount
  if (perVideo === null || remaining <= 0) return null

  if (job.status === 'Queued') {
    const total = formatDuration(perVideo * remaining)
    return remaining > 1 ? `takes about ${total} for ${remaining} videos` : `takes about ${total}`
  }
  if (job.status !== 'Running') return null

  const elapsed = job.currentRunStartedAt ? (now - Date.parse(job.currentRunStartedAt)) / 1000 : 0
  const currentLeft = perVideo - elapsed
  if (currentLeft < 0) return 'taking longer than estimated'
  return `about ${formatDuration(currentLeft + perVideo * (remaining - 1))} left`
}
