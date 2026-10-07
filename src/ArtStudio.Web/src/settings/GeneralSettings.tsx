import { useEffect, useState, type FormEvent } from 'react'
import { api, type Settings } from '../api'
import { ensureNotificationPermission } from '../live/desktopNotifications'

export function GeneralSettings() {
  const [settings, setSettings] = useState<Settings>()
  const [error, setError] = useState<string>()
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    api.settings().then(setSettings, (failure: Error) => setError(failure.message))
  }, [])

  if (!settings) return <section className="panel">{error ? <p className="error">{error}</p> : 'Loading…'}</section>

  function update(changes: Partial<Settings>) {
    setSaved(false)
    setSettings((current) => (current ? { ...current, ...changes } : current))
  }

  async function toggleNotification(key: 'notifyOnJobDone' | 'notifyOnQueueEmpty', enabled: boolean) {
    if (enabled && !(await ensureNotificationPermission())) {
      setError('The browser blocked notifications for this site. Allow them in the site settings first.')
      return
    }
    update({ [key]: enabled })
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!settings) return
    setError(undefined)
    try {
      setSettings(await api.saveSettings(settings))
      setSaved(true)
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  return (
    <form className="panel" onSubmit={(e) => void save(e)}>
      <h3>General</h3>
      <label className="field">
        <span>ComfyUI server URL</span>
        <input value={settings.comfyServerUrl} onChange={(e) => update({ comfyServerUrl: e.target.value })} />
      </label>
      <label className="field">
        <span>Image folder (new images are saved here; existing ones stay where they are)</span>
        <input value={settings.outputDirectory} onChange={(e) => update({ outputDirectory: e.target.value })} />
      </label>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={settings.notifyOnJobDone}
          onChange={(e) => void toggleNotification('notifyOnJobDone', e.target.checked)}
        />
        Browser notification when a job is done
      </label>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={settings.notifyOnQueueEmpty}
          onChange={(e) => void toggleNotification('notifyOnQueueEmpty', e.target.checked)}
        />
        Browser notification when the queue is empty
      </label>
      {error && <p className="error">{error}</p>}
      {saved && <p className="success">Saved.</p>}
      <button type="submit" className="primary">
        Save
      </button>
    </form>
  )
}
