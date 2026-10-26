import { useState } from 'react'
import type { VideoInfo } from '../api'

export function SetVideos({ videos }: { videos: VideoInfo[] }) {
  return (
    <section className="panel set-videos">
      <h3>Videos</h3>
      <div className="video-grid">
        {videos.map((video) => (
          <VideoTile key={video.id} video={video} />
        ))}
      </div>
    </section>
  )
}

function VideoTile({ video }: { video: VideoInfo }) {
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
        </div>
      </figcaption>
    </figure>
  )
}
