import type { Job, JobKind, QueueState } from '../api'

export interface QueueSection {
  kind: JobKind
  title: string
  jobs: Job[]
  paused: boolean
  /** Why waiting jobs don't start although this kind is not paused, if that is the case. */
  waitingNote: string | null
}

const KINDS: { kind: JobKind; title: string; one: string; many: string }[] = [
  { kind: 'Image', title: 'Images', one: 'image', many: 'images' },
  { kind: 'Video', title: 'Videos', one: 'video', many: 'videos' },
]

const isPaused = (state: QueueState, kind: JobKind) => (kind === 'Video' ? state.videosPaused : state.imagesPaused)

/** The kind ComfyUI works on now or next, the way the server picks it: it stays with the loaded model while it can. */
function currentKind(state: QueueState): JobKind {
  const running = state.active.find((job) => job.status === 'Running')
  if (running) return running.kind
  const runnable = state.active.filter((job) => job.status === 'Queued' && !isPaused(state, job.kind))
  if (runnable.some((job) => job.kind === state.loadedKind)) return state.loadedKind!
  return runnable[0]?.kind ?? state.loadedKind ?? 'Image'
}

/**
 * The queue split into images and videos. On a shared ComfyUI server, the kind it is busy with comes first; the other
 * one waits until it is done (or paused), so ComfyUI doesn't switch models back and forth. With a server per kind,
 * both run at once.
 */
export function queueSections(state: QueueState): QueueSection[] {
  const busyKind = state.sharedServer ? currentKind(state) : null
  const ordered = [...KINDS].sort((a, b) => Number(b.kind === busyKind) - Number(a.kind === busyKind))

  return ordered.map(({ kind, title }) => {
    const jobs = state.active.filter((job) => job.kind === kind)
    const other = KINDS.find((k) => k.kind !== kind)!
    const ahead = state.active.filter(
      (job) => job.kind === other.kind && (job.status === 'Running' || (job.status === 'Queued' && !isPaused(state, other.kind))),
    ).length
    const waits = busyKind !== null && kind !== busyKind && !isPaused(state, kind) && jobs.some((job) => job.status === 'Queued') && ahead > 0
    return {
      kind,
      title,
      jobs,
      paused: isPaused(state, kind),
      waitingNote: waits
        ? `Waiting until the ${ahead} ${ahead === 1 ? `${other.one} ahead is` : `${other.many} ahead are`} done, so ComfyUI doesn't switch models.`
        : null,
    }
  })
}

/** The nav badge: which kinds are paused, if any. */
export function pausedLabel(state: QueueState): string | null {
  if (state.imagesPaused && state.videosPaused) return 'paused'
  if (state.imagesPaused) return 'images paused'
  if (state.videosPaused) return 'videos paused'
  return null
}

/** The nav badges: how many images and videos are queued or rendering, failed jobs left out. */
export function pendingCounts(state: QueueState): Record<JobKind, number> {
  const pending = state.active.filter((job) => job.status !== 'Failed')
  return {
    Image: pending.filter((job) => job.kind === 'Image').length,
    Video: pending.filter((job) => job.kind === 'Video').length,
  }
}
