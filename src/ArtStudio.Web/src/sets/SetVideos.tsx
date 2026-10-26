import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { VideoInfo } from '../api'

interface SetVideosProps {
  videos: VideoInfo[]
  canMakeAgain: boolean
  /** The set page's own state, handed on so the Make video page can return to it. */
  setPageState: unknown
}

export function SetVideos({ videos, canMakeAgain, setPageState }: SetVideosProps) {
  return (
    <section className="panel set-videos">
      <h3>Videos</h3>
      <div className="video-grid">
        {videos.map((video) => (
          <VideoTile key={video.id} video={video} canMakeAgain={canMakeAgain} setPageState={setPageState} />
        ))}
      </div>
    </section>
  )
}

interface VideoTileProps {
  video: VideoInfo
  canMakeAgain: boolean
  setPageState: unknown
}

function VideoTile({ video, canMakeAgain, setPageState }: VideoTileProps) {
  const [copied, setCopied] = useState(false)

  async function copyPrompt() {
    await navigator.clipboard.writeText(video.prompt)
    setCopied(true)
  }

  return (
    <figure className="video-tile">
      <video controls preload="metadata" poster={video.startImageUrl} src={video.url} />
      <figcaption>
        <div className="video-tile-frames">
          <img src={video.startImageUrl} alt="Start frame" title="Start frame" />
          {video.endImageUrl && (
            <>
              <span aria-hidden>→</span>
              <img src={video.endImageUrl} alt="End frame" title="End frame" />
            </>
          )}
        </div>
        <div className="job-meta">
          <span>{video.seconds} s</span>
          <span>{video.resolution}</span>
          <span>{video.turbo ? 'turbo' : 'full quality'}</span>
          <span>seed {video.seed}</span>
        </div>
        <div className="job-meta">
          <button type="button" className="link-button" onClick={() => void copyPrompt()}>
            {copied ? 'Copied!' : 'Copy prompt'}
          </button>
          <a href={video.url} target="_blank" rel="noreferrer" className="link-button">
            Open original
          </a>
          {canMakeAgain && (
            <Link
              to={`/sets/${video.setId}/video?from=${video.id}`}
              state={setPageState}
              className="link-button"
              title="Open Make video with this video's settings, to adjust before queueing"
            >
              Make again
            </Link>
          )}
        </div>
      </figcaption>
    </figure>
  )
}
