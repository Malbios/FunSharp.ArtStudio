import { useNavigate } from 'react-router-dom'
import { api } from '../api'
import { useStudioEvents, type JobEventPayload } from './studioHub'

const PROMPT_PREVIEW_LENGTH = 120

export async function ensureNotificationPermission(): Promise<boolean> {
  if (!('Notification' in window)) return false
  if (Notification.permission === 'granted') return true
  return (await Notification.requestPermission()) === 'granted'
}

function canNotify() {
  return 'Notification' in window && Notification.permission === 'granted'
}

export function DesktopNotifications() {
  const navigate = useNavigate()

  useStudioEvents(['JobCompleted', 'QueueEmpty'], (event, payload) => {
    if (event === 'Reconnected' || !canNotify()) return

    void api.settings().then((settings) => {
      if (event === 'JobCompleted' && settings.notifyOnJobDone) {
        const job = payload as JobEventPayload
        // The tag makes several open tabs show one notification instead of one each.
        const notification = new Notification(`Set #${job.setId} is done`, {
          body: job.prompt?.slice(0, PROMPT_PREVIEW_LENGTH),
          tag: `job-${job.jobId}`,
        })
        notification.onclick = () => {
          window.focus()
          navigate(`/sets/${job.setId}`)
        }
      }
      if (event === 'QueueEmpty' && settings.notifyOnQueueEmpty) {
        new Notification('Queue finished', { body: 'All queued jobs are done.', tag: 'queue-empty' })
      }
    })
  })

  return null
}
