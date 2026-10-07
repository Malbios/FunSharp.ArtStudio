import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { useLoad } from '../live/useLoad'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'

export function BlockedArtistsEditor() {
  const artists = useLoad(api.blockedArtists)
  const [username, setUsername] = useState('')
  const [error, setError] = useState<string>()

  async function run(action: () => Promise<unknown>) {
    setError(undefined)
    try {
      await action()
      artists.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function block(event: FormEvent) {
    event.preventDefault()
    await run(async () => {
      await api.blockArtist(username)
      setUsername('')
    })
  }

  return (
    <section className="panel" id="blocked-artists">
      <h3>Blocked DeviantArt users</h3>
      <p className="hint">Deviations from these users cannot be used as a base image.</p>
      <form className="inline-form wide" onSubmit={(e) => void block(e)}>
        <input
          placeholder="Username or profile URL"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <button type="submit" className="primary" disabled={!username.trim()}>
          Block
        </button>
      </form>
      <div className="editable-list">
        {artists.data?.map((artist) => (
          <div key={artist.id} className="blocked-row">
            <DeviantArtUserLink username={artist.username} />
            <button type="button" className="link-button" onClick={() => void run(() => api.unblockArtist(artist.id))}>
              Unblock
            </button>
          </div>
        ))}
        {artists.data?.length === 0 && <p className="hint">Nobody is blocked.</p>}
      </div>
      {(error ?? artists.error) && <p className="error">{error ?? artists.error}</p>}
    </section>
  )
}
