import { useState } from 'react'
import { api, type Job } from '../api'

export function useJobActions(job: Job) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(undefined)
    try {
      await action()
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return {
    busy,
    error,
    canCancel: job.status === 'Queued' || job.status === 'Running',
    canRetry: job.status === 'Failed',
    percent: job.requestedCount === 0 ? 0 : (job.completedCount / job.requestedCount) * 100,
    cancel: () => void run(() => api.cancelJob(job.id)),
    retry: () => void run(() => api.retryJob(job.id)),
  }
}
