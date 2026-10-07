import { Link } from 'react-router-dom'
import { api } from '../api'
import { useStudioEvents } from '../live/studioHub'
import { useLoad } from '../live/useLoad'

export function SetsPage() {
  const sets = useLoad(api.sets)
  useStudioEvents(['JobUpdated', 'ImageAdded'], sets.reload)

  return (
    <div>
      <div className="toolbar">
        <h2>Sets</h2>
      </div>
      {sets.error && <p className="error">{sets.error}</p>}
      {sets.data?.length === 0 && (
        <p className="hint">
          No sets yet. <Link to="/">Create one</Link>.
        </p>
      )}
      <div className="set-grid">
        {sets.data?.map((set) => {
          const thumbnail = set.previewImageUrl ?? set.sourceImageUrl
          return (
            <Link key={set.id} to={`/sets/${set.id}`} className="panel set-card">
              {thumbnail ? <img className="set-card-image" src={thumbnail} alt="" loading="lazy" /> : <div className="set-card-image" />}
              <div className="set-card-body">
                <div className="job-prompt">{set.prompt}</div>
                <div className="job-meta">
                  <span>#{set.id}</span>
                  <span>{set.imageCount} images</span>
                  {set.hasActiveJob && <span className="badge status-Running">generating</span>}
                  {set.hasFailedJob && <span className="badge status-Failed">failed</span>}
                  {set.selectedImageId !== null && <span className="badge status-Completed">picked</span>}
                </div>
              </div>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
