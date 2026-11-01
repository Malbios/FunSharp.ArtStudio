import { useEffect, useState } from 'react'
import { api, type Gallery } from '../api'

export function DefaultGallery() {
  const [galleries, setGalleries] = useState<Gallery[]>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    api.galleries().then(setGalleries, (failure: Error) => setError(failure.message))
  }, [])

  async function choose(folderId: string) {
    if (!galleries) return
    setError(undefined)
    const previous = galleries
    setGalleries(galleries.map((gallery) => ({ ...gallery, isDefault: gallery.folderId === folderId })))
    try {
      setGalleries(await api.saveDefaultGallery(folderId || null))
    } catch (failure) {
      setGalleries(previous)
      setError((failure as Error).message)
    }
  }

  return (
    <div>
      <h4>Default gallery</h4>
      <p className="hint">The publish form starts at this gallery for sets you haven't chosen one for yet.</p>
      {error && <p className="error">{error}</p>}
      <label className="field">
        <select
          aria-label="Default gallery"
          value={galleries?.find((gallery) => gallery.isDefault)?.folderId ?? ''}
          disabled={!galleries}
          onChange={(e) => void choose(e.target.value)}
        >
          <option value="">{galleries ? 'The one published to last' : 'Loading galleries…'}</option>
          {galleries?.map((gallery) => (
            <option key={gallery.folderId} value={gallery.folderId}>
              {gallery.name}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
