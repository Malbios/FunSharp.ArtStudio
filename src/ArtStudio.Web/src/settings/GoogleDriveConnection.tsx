import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api, type GoogleApp } from '../api'
import { ComfyOutputFolderCleaner } from './ComfyOutputFolderCleaner'

const CLOUD_CONSOLE_URL = 'https://console.cloud.google.com/'
const TESTING_LOGIN_DAYS = 7

function loginEndsAround(connectedAt: string): string {
  const end = new Date(connectedAt)
  end.setDate(end.getDate() + TESTING_LOGIN_DAYS)
  return end.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' })
}

export function GoogleDriveConnection() {
  const [searchParams] = useSearchParams()
  const [app, setApp] = useState<GoogleApp>()
  const [clientId, setClientId] = useState('')
  const [clientSecret, setClientSecret] = useState('')
  const [outputFolder, setOutputFolder] = useState('')
  const [error, setError] = useState<string>()
  const [saved, setSaved] = useState(false)

  const loginOutcome = searchParams.get('google')
  const loginMessage = searchParams.get('message')

  useEffect(() => {
    api.googleApp().then((loaded) => {
      setApp(loaded)
      setClientId(loaded.clientId ?? '')
      setOutputFolder(loaded.outputFolder)
    }, (failure: Error) => setError(failure.message))
  }, [])

  async function save(event: FormEvent) {
    event.preventDefault()
    setError(undefined)
    setSaved(false)
    try {
      const updated = await api.saveGoogleApp(clientId, clientSecret, outputFolder)
      setApp(updated)
      setOutputFolder(updated.outputFolder)
      setClientSecret('')
      setSaved(true)
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function disconnect() {
    setError(undefined)
    try {
      await api.disconnectGoogle()
      setApp(await api.googleApp())
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  if (!app) return <section className="panel">{error ? <p className="error">{error}</p> : 'Loading…'}</section>

  return (
    <section className="panel" id="google-drive">
      <h3>Google Drive</h3>
      <p className="hint">
        Finished videos are downloaded from ComfyUI's output folder on Google Drive instead of through ComfyUI, so they
        don't count against ngrok's data transfer.
      </p>

      {loginOutcome === 'connected' && app.connected && <p className="success">Google Drive is connected.</p>}
      {loginOutcome === 'error' && <p className="error">Google login failed: {loginMessage}</p>}

      <p>
        {app.connected ? (
          <>
            Connected as <strong>{app.account ?? 'unknown account'}</strong>
            {app.connectedAt && (
              <>
                {' '}
                since {new Date(app.connectedAt).toLocaleString()}. While the Google app is in Testing, Google ends the
                login after {TESTING_LOGIN_DAYS} days, around <strong>{loginEndsAround(app.connectedAt)}</strong>.
              </>
            )}{' '}
            <button type="button" className="link-button" onClick={() => void disconnect()}>
              Disconnect
            </button>
          </>
        ) : (
          'Not connected.'
        )}
      </p>

      <details open={!app.hasClientSecret}>
        <summary>Setup</summary>
        <ol className="hint">
          <li>
            In the{' '}
            <a href={CLOUD_CONSOLE_URL} target="_blank" rel="noreferrer">
              Google Cloud Console
            </a>
            , enable the <strong>Google Drive API</strong> for your project.
          </li>
          <li>On the OAuth consent screen (Audience), add your Google account as a test user.</li>
          <li>
            Create an OAuth client of type <strong>Desktop app</strong>. If Google refuses the login's redirect, create
            a <strong>Web application</strong> client instead and add this redirect URI: <code>{app.redirectUri}</code>
          </li>
          <li>Paste its Client ID and Client Secret below, save, then connect.</li>
        </ol>
        <form onSubmit={(e) => void save(e)}>
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
          <label className="field">
            <span>ComfyUI's output folder on Drive (from My Drive)</span>
            <input value={outputFolder} onChange={(e) => setOutputFolder(e.target.value)} placeholder="ComfyUI/output" />
          </label>
          <button type="submit" disabled={!clientId.trim()}>
            Save
          </button>
          {saved && <span className="success"> Saved.</span>}
        </form>
      </details>

      {app.connected && <ComfyOutputFolderCleaner />}

      {error && <p className="error">{error}</p>}

      <p>
        <a className="button-link inline" href={api.googleLoginUrl}>
          {app.connected ? 'Reconnect Google Drive' : 'Connect Google Drive'}
        </a>
      </p>
    </section>
  )
}
