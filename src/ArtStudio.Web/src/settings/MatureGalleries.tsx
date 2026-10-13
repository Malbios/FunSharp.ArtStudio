import { useEffect, useState } from 'react'
import { api, type Gallery } from '../api'

export function MatureGalleries() {
  const [galleries, setGalleries] = useState<Gallery[]>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    api.galleries().then(setGalleries, (failure: Error) => setError(failure.message))
  }, [])

  async function toggle(folderId: string) {
    if (!galleries) return
    setError(undefined)
    const previous = galleries
    const changed = galleries.map((gallery) =>
      gallery.folderId === folderId ? { ...gallery, mature: !gallery.mature } : gallery,
    )
    setGalleries(changed)
    try {
      setGalleries(await api.saveMatureGalleries(changed.filter((gallery) => gallery.mature).map((gallery) => gallery.folderId)))
    } catch (failure) {
      setGalleries(previous)
      setError((failure as Error).message)
    }
  }

  return (
    <div>
      <h4>Mature galleries</h4>
      <p className="hint">Posting to a gallery ticked here marks the deviation as mature by default.</p>
      {error && <p className="error">{error}</p>}
      {!galleries && !error && <p className="hint">Loading galleries…</p>}
      {galleries?.length === 0 && <p className="hint">Your account has no galleries.</p>}
      {galleries?.map((gallery) => (
        <label key={gallery.folderId} className="checkbox">
          <input type="checkbox" checked={gallery.mature} onChange={() => void toggle(gallery.folderId)} />
          {gallery.name}
        </label>
      ))}
    </div>
  )
}
