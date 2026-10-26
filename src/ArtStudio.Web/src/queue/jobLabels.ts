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
