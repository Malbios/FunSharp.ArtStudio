import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type VideoInfo } from '../api'

interface SetVideosProps {
  videos: VideoInfo[]
  canMakeAgain: boolean
  /** The set page's own state, handed on so the Make video page can return to it. */
  setPageState: unknown
  onChanged: () => void
  onError: (message: string) => void
}

export function SetVideos({ videos, ...tileProps }: SetVideosProps) {
  return (
    <section className="panel set-videos">
      <h3>Videos</h3>
      <div className="video-grid">
        {videos.map((video) => (
          <VideoTile key={video.id} video={video} {...tileProps} />
        ))}
      </div>
    </section>
  )
}

type VideoTileProps = Omit<SetVideosProps, 'videos'> & { video: VideoInfo }

function VideoTile({ video, canMakeAgain, setPageState, onChanged, onError }: VideoTileProps) {
  const [copied, setCopied] = useState(false)

  async function copyPrompt() {
    await navigator.clipboard.writeText(video.prompt)
    setCopied(true)
  }

  async function togglePick() {
    try {
      await api.pickVideo(video.id, !video.picked)
      onChanged()
    } catch (failure) {
      onError((failure as Error).message)
    }
  }

  async function markViewed() {
    if (video.viewed) return
    try {
      await api.markVideoViewed(video.id)
      onChanged()
    } catch (failure) {
      onError((failure as Error).message)
    }
  }

  return (
    <figure className={video.picked ? 'video-tile selected' : 'video-tile'}>
      <video
        controls
        preload="metadata"
        poster={video.startImageUrl}
        src={video.url}
        onPlay={() => void markViewed()}
      />
      <button
        type="button"
        className="image-tile-pick"
        title={video.picked ? 'Remove from picks' : 'Pick this video'}
        aria-pressed={video.picked}
        onClick={() => void togglePick()}
      >
        {video.picked ? '★' : '☆'}
      </button>
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
          {!video.viewed && <span className="badge new-videos">new</span>}
          <span>{video.seconds} s</span>
          <span>{video.resolution}</span>
          <span>{video.turbo ? 'turbo' : 'full quality'}</span>
          <span>seed {video.seed}</span>
        </div>
        <div className="job-meta">
          <button type="button" className="link-button" onClick={() => void copyPrompt()}>
            {copied ? 'Copied!' : 'Copy prompt'}
          </button>
          <a
            href={video.url}
            target="_blank"
            rel="noreferrer"
            className="link-button"
            onClick={() => void markViewed()}
          >
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
