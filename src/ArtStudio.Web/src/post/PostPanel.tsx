import { useEffect, useState, type FormEvent } from 'react'
import { api, type Gallery, type MatureLevel, type SetDetail } from '../api'
import { hasOnlyDefaultTags, initialPostFields, MAX_TITLE_LENGTH, stashDescription, stashEditorUrl, withSuggestedTags } from './postDefaults'

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
  const [suggestingTags, setSuggestingTags] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string>()

  const backgroundSuggesting = set.post.suggesting
  const [saved, setSaved] = useState({ title: set.post.title, tags: set.post.tags })
  if (saved.title !== set.post.title || saved.tags !== set.post.tags) {
    const { title, tags } = set.post
    setSaved({ title, tags })
    setFields((current) => ({
      ...current,
      title: !current.title && title ? title : current.title,
      tags: hasOnlyDefaultTags(current.tags) && tags ? tags : current.tags,
    }))
  }

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

  async function suggestTags() {
    setSuggestingTags(true)
    setError(undefined)
    try {
      const { tags } = await api.suggestTags(set.id)
      setFields((current) => ({ ...current, tags: withSuggestedTags(current.tags, tags) }))
    } catch (failure) {
      setError(`No tag suggestions: ${(failure as Error).message}`)
    } finally {
      setSuggestingTags(false)
    }
  }

  async function upload(event: FormEvent) {
    event.preventDefault()
    setUploading(true)
    setError(undefined)
    try {
      await api.stash(set.id, fields.title, stashDescription(fields.description, set.deviantArtUrl), fields.tags)
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
      {backgroundSuggesting && <p className="hint">Suggesting a title and tags…</p>}
      <div className="field">
        <OriginalTitle set={set} />
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
          <button type="button" disabled={suggesting || backgroundSuggesting} onClick={() => void suggestTitle()}>
            {suggesting ? 'Suggesting…' : 'Suggest title'}
          </button>
        </div>
      </div>
      <label className="field">
        <span>Description{set.deviantArtUrl ? ' ("Inspired by" is added at the end on upload)' : ''}</span>
        <textarea rows={3} value={fields.description} onChange={(e) => setFields({ ...fields, description: e.target.value })} />
      </label>
      <div className="field">
        <span>Tags (separated by spaces; letters, digits and _ only)</span>
        <div className="post-title-row">
          <input aria-label="Tags" value={fields.tags} onChange={(e) => setFields({ ...fields, tags: e.target.value })} />
          <button type="button" disabled={suggestingTags || backgroundSuggesting} onClick={() => void suggestTags()}>
            {suggestingTags ? 'Suggesting…' : 'Suggest tags'}
          </button>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      <button type="submit" className="success" disabled={uploading || title.length === 0}>
        {uploading ? 'Uploading…' : 'Upload #1 to Stash'}
      </button>
    </form>
  )
}

/** The inspiration's own title, for reference while writing this one. */
function OriginalTitle({ set }: { set: SetDetail }) {
  const [fetchedTitle, setFetchedTitle] = useState<string | null>(null)
  const needsFetch = set.deviantArtUrl !== null && set.deviantArtTitle === null

  useEffect(() => {
    if (!needsFetch) return
    let current = true
    api.deviantArtTitle(set.id).then(
      ({ title }) => current && setFetchedTitle(title),
      () => {},
    )
    return () => {
      current = false
    }
  }, [needsFetch, set.id])

  const title = set.deviantArtTitle ?? fetchedTitle
  if (!set.deviantArtUrl || !title) return null
  return (
    <p className="hint post-original-title">
      Original title on DeviantArt:{' '}
      <a href={set.deviantArtUrl} target="_blank" rel="noreferrer">
        „{title}“
      </a>
    </p>
  )
}

function ExtraPicksButton({ set, onError }: { set: SetDetail; onError: (message: string | undefined) => void }) {
  const [folder, setFolder] = useState<string>()
  const extraPicks = set.pickedImageIds.length - 1

  async function openExtraPicks() {
    onError(undefined)
    try {
      setFolder((await api.openExtraPicks(set.id)).folder)
    } catch (failure) {
      onError((failure as Error).message)
    }
  }

  return (
    <>
      <button type="button" onClick={() => void openExtraPicks()}>
        Open picks #2{extraPicks > 1 ? `–#${extraPicks + 1}` : ''} folder
      </button>
      {folder && <span className="hint"> Opened {folder}</span>}
    </>
  )
}

function InStash({ set, stashItemId, onChanged, onPublished }: PostPanelProps & { stashItemId: string }) {
  const [error, setError] = useState<string>()
  const [linkCopied, setLinkCopied] = useState(false)
  const extraPicks = set.pickedImageIds.length - 1

  async function copyInspirationLink() {
    if (!set.deviantArtUrl) return
    try {
      await navigator.clipboard.writeText(set.deviantArtUrl)
      setLinkCopied(true)
    } catch {
      setError('Could not copy the inspiration link to the clipboard.')
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
            <ExtraPicksButton set={set} onError={setError} />
          </li>
        )}
        <li>
          <a href={stashEditorUrl(stashItemId)} target="_blank" rel="noreferrer" onClick={() => void copyInspirationLink()}>
            Open in DeviantArt's editor
          </a>
          {extraPicks > 0 ? ', add the other picks in order' : ''}
          {set.deviantArtUrl ? ', paste the copied inspiration link after "Inspired by"' : ''}
          {extraPicks > 0 || set.deviantArtUrl ? ' and save.' : ' to check it.'}
          {linkCopied && <span className="hint"> Copied the inspiration link to the clipboard.</span>}
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
    if (!galleryId) return
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
    <div>
      <p>
        <strong>{set.post.title}</strong> was published
        {set.post.publishedAt && ` on ${new Date(set.post.publishedAt).toLocaleString()}`}:{' '}
        <a href={set.post.publishedUrl!} target="_blank" rel="noreferrer">
          view on DeviantArt
        </a>
        .
      </p>
    </div>
  )
}
