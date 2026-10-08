import { useEffect, useRef } from 'react'
import { api } from '../api'

const TYPING_PAUSE_MS = 500

interface PendingSave {
  timer: number
  run: () => void
}

/**
 * Saves a draft's resolution and image count as they change. `saveSoon` waits for a typing pause; a pending save
 * still happens when the page is left or switches to another draft. Does nothing when `draftId` is null.
 */
export function useDraftSettingsAutosave(draftId: number | null, onError: (message: string) => void) {
  const pending = useRef<PendingSave>(undefined)

  function cancel() {
    if (pending.current) window.clearTimeout(pending.current.timer)
    pending.current = undefined
  }

  function save(resolution: string, imageCount: number) {
    cancel()
    if (draftId === null || !resolution) return
    api.saveDraftSettings(draftId, resolution, imageCount).catch((failure: Error) => onError(failure.message))
  }

  function saveSoon(resolution: string, imageCount: number) {
    cancel()
    if (draftId === null) return
    const run = () => save(resolution, imageCount)
    pending.current = { timer: window.setTimeout(run, TYPING_PAUSE_MS), run }
  }

  useEffect(() => () => pending.current?.run(), [draftId])

  return { save, saveSoon, cancel }
}
