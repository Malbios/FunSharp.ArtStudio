import { useEffect, useState, type FormEvent } from 'react'
import { api, type Gallery, type MatureLevel, type SetDetail } from '../api'
import { initialPostFields, MAX_TITLE_LENGTH, stashEditorUrl } from './postDefaults'

interface PostPanelProps {
  set: SetDetail
  onChanged: () => void
  onPublished: () => void
}

export function PostPanel({ set, onChanged, onPublished }: PostPanelProps) {
  return (
    <section className="panel post-panel">
      <h3>Post to DeviantArt</h3>
      {set.post.publishedUrl ? (
        <Published set={set} />
      ) : set.post.stashItemId ? (
        <InStash set={set} stashItemId={set.post.stashItemId} onChanged={onChanged} onPublished={onPublished} />
      ) : (
        <StashForm set={set} onChanged={onChanged} />
      )}
    </section>
  )
}

function StashForm({ set, onChanged }: Pick<PostPanelProps, 'set' | 'onChanged'>) {
  const [fields, setFields] = useState(() => initialPostFields(set))
  const [suggesting, setSuggesting] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string>()

  async function suggestTitle() {
    setSuggesting(true)
    setError(undefined)
    try {
      const { title } = await api.suggestTitle(set.id)
      setFields((current) => ({ ...current, title }))
    } catch (failure) {
      setError(`No title suggestion: ${(failure as Error).message}`)
    } finally {
      setSuggesting(false)
    }
  }

  async function upload(event: FormEvent) {
    event.preventDefault()
    setUploading(true)
    setError(undefined)
    try {
      await api.stash(set.id, fields.title, fields.description, fields.tags)
      onChanged()
    } catch (failure) {
      setError((failure as Error).message)
      setUploading(false)
    }
  }

  const title = fields.title.trim()
  return (
    <form onSubmit={(e) => void upload(e)}>
      <p className="hint">
        Uploads pick #1 to your private Stash. DeviantArt's API takes one image per deviation, so the other picks are
        added by hand in DeviantArt's editor afterwards.
      </p>
      <div className="field">
        <span>
          Title ({fields.title.length}/{MAX_TITLE_LENGTH})
        </span>
        <div className="post-title-row">
          <input
            aria-label="Title"
            value={fields.title}
            maxLength={MAX_TITLE_LENGTH}
            placeholder={suggesting ? 'Asking the vision model for a title…' : ''}
            onChange={(e) => setFields({ ...fields, title: e.target.value })}
          />
          <button type="button" disabled={suggesting} onClick={() => void suggestTitle()}>
            {suggesting ? 'Suggesting…' : 'Suggest title'}
          </button>
        </div>
      </div>
      <label className="field">
        <span>Description</span>
        <textarea rows={3} value={fields.description} onChange={(e) => setFields({ ...fields, description: e.target.value })} />
      </label>
      <label className="field">
        <span>Tags (separated by spaces; letters, digits and _ only)</span>
        <input value={fields.tags} onChange={(e) => setFields({ ...fields, tags: e.target.value })} />
      </label>
      {error && <p className="error">{error}</p>}
      <button type="submit" className="success" disabled={uploading || title.length === 0}>
        {uploading ? 'Uploading…' : 'Upload #1 to Stash'}
      </button>
    </form>
  )
}

function InStash({ set, stashItemId, onChanged, onPublished }: PostPanelProps & { stashItemId: string }) {
  const [error, setError] = useState<string>()
  const [folder, setFolder] = useState<string>()
  const extraPicks = set.pickedImageIds.length - 1

  async function openExtraPicks() {
    setError(undefined)
    try {
      setFolder((await api.openExtraPicks(set.id)).folder)
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function forget() {
    const confirmed = window.confirm(
      'Forget the Stash item? Art Studio cannot delete it on DeviantArt, so delete it there yourself if you no longer need it.',
    )
    if (!confirmed) return
    setError(undefined)
    try {
      await api.forgetStash(set.id)
      onChanged()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  return (
    <div>
      <p>
        <strong>{set.post.title}</strong> is in your Stash
        {set.post.stashedAt && ` since ${new Date(set.post.stashedAt).toLocaleString()}`}.
      </p>
      <ol className="post-steps">
        {extraPicks > 0 && (
          <li>
            <button type="button" onClick={() => void openExtraPicks()}>
              Open picks #2{extraPicks > 1 ? `–#${extraPicks + 1}` : ''} folder
            </button>
            {folder && <span className="hint"> Opened {folder}</span>}
          </li>
        )}
        <li>
          <a href={stashEditorUrl(stashItemId)} target="_blank" rel="noreferrer">
            Open in DeviantArt's editor
          </a>
          {extraPicks > 0 ? ', add the other picks in order and save.' : ' to check it.'}
        </li>
      </ol>
      {error && <p className="error">{error}</p>}
      <PublishForm set={set} onPublished={onPublished} />
      <button type="button" className="link-button" onClick={() => void forget()}>
        Forget Stash item
      </button>
    </div>
  )
}

function PublishForm({ set, onPublished }: Pick<PostPanelProps, 'set' | 'onPublished'>) {
  const [galleries, setGalleries] = useState<Gallery[]>()
  const [galleryId, setGalleryId] = useState('')
  const [isMature, setIsMature] = useState(false)
  const [matureLevel, setMatureLevel] = useState<MatureLevel>('moderate')
  const [publishing, setPublishing] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    api.galleries().then((loaded) => {
      setGalleries(loaded)
      const lastUsed = loaded.find((gallery) => gallery.lastUsed)
      if (lastUsed) chooseGallery(lastUsed)
    }, (failure: Error) => setError(failure.message))
  }, [])

  function chooseGallery(gallery: Gallery | undefined) {
    setGalleryId(gallery?.folderId ?? '')
    setIsMature(gallery?.mature ?? false)
  }

  async function publish(event: FormEvent) {
    event.preventDefault()
    const gallery = galleries?.find((candidate) => candidate.folderId === galleryId)
    if (!gallery) return
    const rating = isMature ? `mature (${matureLevel})` : 'not mature'
    if (!window.confirm(`Publish "${set.post.title}" to ${gallery.name}, ${rating}? It goes live on DeviantArt right away.`)) return
    setPublishing(true)
    setError(undefined)
    try {
      await api.publish(set.id, galleryId, isMature, isMature ? matureLevel : null)
      onPublished()
    } catch (failure) {
      setError((failure as Error).message)
      setPublishing(false)
    }
  }

  return (
    <form className="publish-form" onSubmit={(e) => void publish(e)}>
      <div className="field-row">
        <label className="field">
          <span>Gallery</span>
          <select
            value={galleryId}
            disabled={!galleries}
            onChange={(e) => chooseGallery(galleries?.find((gallery) => gallery.folderId === e.target.value))}
          >
            <option value="">{galleries ? 'Choose a gallery…' : 'Loading galleries…'}</option>
            {galleries?.map((gallery) => (
              <option key={gallery.folderId} value={gallery.folderId}>
                {gallery.name}
                {gallery.mature ? ' (mature)' : ''}
              </option>
            ))}
          </select>
        </label>
        <label className="field narrow">
          <span>Mature</span>
          <select
            aria-label="Mature level"
            value={isMature ? matureLevel : 'no'}
            onChange={(e) => {
              setIsMature(e.target.value !== 'no')
              if (e.target.value !== 'no') setMatureLevel(e.target.value as MatureLevel)
            }}
          >
            <option value="no">No</option>
            <option value="moderate">Moderate</option>
            <option value="strict">Strict</option>
          </select>
        </label>
      </div>
      {error && <p className="error">{error}</p>}
      <button type="submit" className="success" disabled={publishing || !galleryId}>
        {publishing ? 'Publishing…' : 'Publish'}
      </button>
    </form>
  )
}

function Published({ set }: Pick<PostPanelProps, 'set'>) {
  return (
    <p>
      <strong>{set.post.title}</strong> was published
      {set.post.publishedAt && ` on ${new Date(set.post.publishedAt).toLocaleString()}`}:{' '}
      <a href={set.post.publishedUrl!} target="_blank" rel="noreferrer">
        view on DeviantArt
      </a>
      .
    </p>
  )
}
