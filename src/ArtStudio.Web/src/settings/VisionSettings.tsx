import { useEffect, useState, type FormEvent } from 'react'
import { api, type VisionSettings as VisionSettingsData } from '../api'

export function VisionSettings() {
  const [settings, setSettings] = useState<VisionSettingsData>()
  const [apiKey, setApiKey] = useState('')
  const [error, setError] = useState<string>()
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    api.visionSettings().then(setSettings, (failure: Error) => setError(failure.message))
  }, [])

  async function change(action: () => Promise<VisionSettingsData>) {
    setError(undefined)
    setSaved(false)
    try {
      setSettings(await action())
      setApiKey('')
      setSaved(true)
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  function save(event: FormEvent) {
    event.preventDefault()
    void change(() => api.saveVisionApiKey(apiKey))
  }

  if (!settings) return <section className="panel">{error ? <p className="error">{error}</p> : 'Loading…'}</section>

  return (
    <section className="panel" id="vision">
      <h3>Vision model</h3>
      <p className="hint">
        Generates prompts for new drafts from their image. The SSH tunnel to the vision server has to be running.
      </p>
      <form onSubmit={save}>
        <label className="field">
          <span>API key {settings.hasApiKey && '(saved; leave empty to keep it)'}</span>
          <input type="password" value={apiKey} onChange={(e) => setApiKey(e.target.value)} autoComplete="off" />
        </label>
        <div className="inline-form">
          <button type="submit" disabled={!apiKey.trim()}>
            Save API key
          </button>
          {settings.hasApiKey && (
            <button type="button" onClick={() => void change(api.removeVisionApiKey)}>
              Remove API key
            </button>
          )}
          {saved && <span className="success">Saved.</span>}
        </div>
      </form>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
