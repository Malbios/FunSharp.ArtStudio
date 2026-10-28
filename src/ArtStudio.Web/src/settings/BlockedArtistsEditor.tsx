import { useState, type FormEvent } from 'react'
import { api, type BlockArtistResult } from '../api'
import { useLoad } from '../live/useLoad'
import { DeviantArtUserLink } from '../deviantart/DeviantArtUserLink'

const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`

function blockSummary({ artist, archivedSets, deletedDrafts }: BlockArtistResult) {
  const done = [
    archivedSets > 0 && `archived ${plural(archivedSets, 'set')}`,
    deletedDrafts > 0 && `deleted ${plural(deletedDrafts, 'draft')}`,
  ].filter(Boolean)
  return done.length === 0 ? null : `Blocked ${artist.username}: ${done.join(' and ')}.`
}

export function BlockedArtistsEditor() {
  const artists = useLoad(api.blockedArtists)
  const [username, setUsername] = useState('')
  const [error, setError] = useState<string>()
  const [lastBlock, setLastBlock] = useState<BlockArtistResult>()

  async function run(action: () => Promise<unknown>) {
    setError(undefined)
    setLastBlock(undefined)
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
      setLastBlock(await api.blockArtist(username))
      setUsername('')
    })
  }

  const summary = lastBlock && blockSummary(lastBlock)

  return (
    <section className="panel" id="blocked-artists">
      <h3>Blocked DeviantArt users</h3>
      <p className="hint">
        Deviations from these users cannot be used as a base image. Blocking someone also archives their sets and
        deletes their drafts.
      </p>
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
      {summary && <p className="hint">{summary}</p>}
      {lastBlock && lastBlock.postedUrls.length > 0 && (
        <div className="hint">
          DeviantArt's API can't unpublish posts, so delete these yourself:
          <ul>
            {lastBlock.postedUrls.map((url) => (
              <li key={url}>
                <a href={url} target="_blank" rel="noreferrer">
                  {url}
                </a>
              </li>
            ))}
          </ul>
        </div>
      )}
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
