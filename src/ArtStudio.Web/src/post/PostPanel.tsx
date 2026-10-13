import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { api, type SetDetail } from '../api'
import { initialPostFields, MAX_TITLE_LENGTH, stashEditorUrl } from './postDefaults'

interface PostPanelProps {
  set: SetDetail
  onChanged: () => void
}

export function PostPanel({ set, onChanged }: PostPanelProps) {
  return (
    <section className="panel post-panel">
      <h3>Post to DeviantArt</h3>
      {set.post.stashItemId ? (
        <InStash set={set} stashItemId={set.post.stashItemId} onChanged={onChanged} />
      ) : (
        <StashForm set={set} onChanged={onChanged} />
      )}
    </section>
  )
}

function StashForm({ set, onChanged }: PostPanelProps) {
  const [fields, setFields] = useState(() => initialPostFields(set))
  const [suggesting, setSuggesting] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string>()
  const openedWithTitle = useRef<boolean>(undefined)
  openedWithTitle.current ??= fields.title !== ''

  const suggestTitle = useCallback(async (keepTypedTitle: boolean) => {
    setSuggesting(true)
    setError(undefined)
    try {
      const { title } = await api.suggestTitle(set.id)
      setFields((current) => (keepTypedTitle && current.title ? current : { ...current, title }))
    } catch (failure) {
      setError(`No title suggestion: ${(failure as Error).message}`)
    } finally {
      setSuggesting(false)
    }
  }, [set.id])

  useEffect(() => {
    if (!openedWithTitle.current) void suggestTitle(true)
  }, [suggestTitle])

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
          <button type="button" disabled={suggesting} onClick={() => void suggestTitle(false)}>
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

function InStash({ set, stashItemId, onChanged }: PostPanelProps & { stashItemId: string }) {
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
      <button type="button" className="link-button" onClick={() => void forget()}>
        Forget Stash item
      </button>
    </div>
  )
}
