import { Link } from 'react-router-dom'

/** Switches the New page between a new image prompt and a new video. */
export function NewModeTabs({ video }: { video: boolean }) {
  return (
    <div className="tabs new-mode-tabs" role="tablist" aria-label="What to make">
      <Link to="/" role="tab" aria-selected={!video} className={video ? 'tab' : 'tab active'}>
        Images
      </Link>
      <Link to="/?mode=video" role="tab" aria-selected={video} className={video ? 'tab active' : 'tab'}>
        Video
      </Link>
    </div>
  )
}
