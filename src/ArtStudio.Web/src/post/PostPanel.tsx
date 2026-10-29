import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, type Gallery, type PublishMatureLevel, type SetDetail } from '../api'
import {
  hasOnlyDefaultTags,
  initialPostFields,
  initialPublishChoice,
  matureLevelOf,
  MAX_TITLE_LENGTH,
  stashDescription,
  stashEditorUrl,
  withSuggestedTags,
  type PublishChoice,
} from './postDefaults'

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
        <li>
          <a href={stashEditorUrl(stashItemId)} target="_blank" rel="noreferrer" onClick={() => void copyInspirationLink()}>
            Open in DeviantArt's editor
          </a>
          {extraPicks > 0
            ? `, add ${extraPicks === 1 ? 'pick #2' : `picks #2–#${extraPicks + 1}`} with the Art Studio box there (browser extension)`
            : ''}
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
  const [choice, setChoice] = useState<PublishChoice>({ galleryId: '', matureLevel: 'none' })
  const [publishing, setPublishing] = useState(false)
  const [error, setError] = useState<string>()
  const savingRef = useRef(Promise.resolve())
  const { galleryId, matureLevel } = choice

  useEffect(() => {
    api.galleries().then((loaded) => {
      setGalleries(loaded)
      setChoice(initialPublishChoice(loaded, set.post))
    }, (failure: Error) => setError(failure.message))
    // Only on opening the set: later changes come from this form itself.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [set.id])

  // One save at a time, so quick changes can't arrive out of order and leave an older choice saved.
  function change(next: PublishChoice) {
    setChoice(next)
    setError(undefined)
    savingRef.current = savingRef.current
      .then(() => api.savePublishChoice(set.id, next.galleryId, next.matureLevel))
      .catch((failure: Error) => setError(failure.message))
  }

  async function publish(event: FormEvent) {
    event.preventDefault()
    if (!galleryId) return
    setPublishing(true)
    setError(undefined)
    try {
      const isMature = matureLevel !== 'none'
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
            onChange={(e) => {
              const gallery = galleries?.find((candidate) => candidate.folderId === e.target.value)
              change({ galleryId: gallery?.folderId ?? '', matureLevel: matureLevelOf(gallery) })
            }}
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
            value={matureLevel}
            onChange={(e) => change({ galleryId, matureLevel: e.target.value as PublishMatureLevel })}
          >
            <option value="none">No</option>
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
