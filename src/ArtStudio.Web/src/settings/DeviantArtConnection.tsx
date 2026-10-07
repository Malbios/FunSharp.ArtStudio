import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api, type DeviantArtApp } from '../api'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'

const DEVELOPER_APPS_URL = 'https://www.deviantart.com/developers/apps'

export function DeviantArtConnection() {
  const [searchParams] = useSearchParams()
  const [app, setApp] = useState<DeviantArtApp>()
  const [clientId, setClientId] = useState('')
  const [clientSecret, setClientSecret] = useState('')
  const [error, setError] = useState<string>()
  const [saved, setSaved] = useState(false)

  const loginOutcome = searchParams.get('deviantart')
  const loginMessage = searchParams.get('message')

  useEffect(() => {
    api.deviantArtApp().then((loaded) => {
      setApp(loaded)
      setClientId(loaded.clientId ?? '')
    }, (failure: Error) => setError(failure.message))
  }, [])

  async function saveCredentials(event: FormEvent) {
    event.preventDefault()
    setError(undefined)
    setSaved(false)
    try {
      setApp(await api.saveDeviantArtApp(clientId, clientSecret))
      setClientSecret('')
      setSaved(true)
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function disconnect() {
    setError(undefined)
    try {
      await api.disconnectDeviantArt()
      setApp(await api.deviantArtApp())
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  if (!app) return <section className="panel">{error ? <p className="error">{error}</p> : 'Loading…'}</section>

  return (
    <section className="panel" id="deviantart">
      <h3>DeviantArt</h3>
      <p className="hint">
        Connecting your account lets the app fetch mature deviations without DeviantArt's blur, and is needed later
        for uploading drafts.
      </p>

      {loginOutcome === 'connected' && app.connected && <p className="success">DeviantArt is connected.</p>}
      {loginOutcome === 'error' && <p className="error">DeviantArt login failed: {loginMessage}</p>}

      <p>
        {app.connected ? (
          <>
            Connected as <strong>{app.username ? <DeviantArtUserLink username={app.username} /> : 'unknown user'}</strong>.{' '}
            <button type="button" className="link-button" onClick={() => void disconnect()}>
              Disconnect
            </button>
          </>
        ) : (
          'Not connected.'
        )}
      </p>

      <details open={!app.hasClientSecret}>
        <summary>App setup</summary>
        <ol className="hint">
          <li>
            Register an app at{' '}
            <a href={DEVELOPER_APPS_URL} target="_blank" rel="noreferrer">
              {DEVELOPER_APPS_URL}
            </a>
            .
          </li>
          <li>
            Add this exact redirect URI to its whitelist: <code>{app.redirectUri}</code>
          </li>
          <li>Paste the Client ID and Client Secret below, save, then connect.</li>
        </ol>
        <form onSubmit={(e) => void saveCredentials(e)}>
          <div className="field-row">
            <label className="field">
              <span>Client ID</span>
              <input value={clientId} onChange={(e) => setClientId(e.target.value)} />
            </label>
            <label className="field">
              <span>Client Secret {app.hasClientSecret && '(saved; leave empty to keep it)'}</span>
              <input type="password" value={clientSecret} onChange={(e) => setClientSecret(e.target.value)} />
            </label>
          </div>
          <button type="submit" disabled={!clientId.trim()}>
            Save app credentials
          </button>
          {saved && <span className="success"> Saved.</span>}
        </form>
      </details>

      {error && <p className="error">{error}</p>}

      <p>
        <a className="button-link inline" href={api.deviantArtLoginUrl}>
          {app.connected ? 'Reconnect DeviantArt' : 'Connect DeviantArt'}
        </a>
      </p>
    </section>
  )
}
