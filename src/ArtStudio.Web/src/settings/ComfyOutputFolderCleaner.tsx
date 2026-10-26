import { useState } from 'react'
import { api, type OutputFolderContents } from '../api'

function describe(contents: OutputFolderContents): string {
  const megabytes = contents.totalBytes / 1024 / 1024
  return `${contents.fileCount} file${contents.fileCount === 1 ? '' : 's'}, ${megabytes.toFixed(1)} MB`
}

/** Checks how much ComfyUI's output folder on Drive holds, and moves it all to Drive's trash on request. */
export function ComfyOutputFolderCleaner() {
  const [contents, setContents] = useState<OutputFolderContents>()
  const [cleared, setCleared] = useState<OutputFolderContents>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(undefined)
    try {
      await action()
    } catch (failure) {
      setError((failure as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const measure = () =>
    run(async () => {
      setCleared(undefined)
      setContents(await api.comfyOutputFolder())
    })

  const clear = () =>
    run(async () => {
      const current = await api.comfyOutputFolder()
      const confirmed = window.confirm(
        `Move ${describe(current)} in "${current.folder}" and its subfolders to Google Drive's trash? ` +
          'They stay restorable from the trash for 30 days. Art Studio keeps its own copies.',
      )
      if (!confirmed) return setContents(current)
      setCleared(await api.clearComfyOutputFolder())
      setContents(undefined)
    })

  return (
    <div className="output-folder-cleaner">
      <h4>ComfyUI's output folder</h4>
      <p className="hint">
        Every image and video ComfyUI renders stays in its output folder on Drive. Art Studio keeps its own copies, so
        the folder can be cleared. Not while a job is running or a video still has to be downloaded.
      </p>
      <div className="toolbar">
        <button type="button" disabled={busy} onClick={() => void measure()}>
          Check size
        </button>
        <button type="button" className="danger" disabled={busy} onClick={() => void clear()}>
          Move everything to Drive's trash
        </button>
      </div>
      {contents && (
        <p>
          <strong>{contents.folder}</strong> holds {describe(contents)}.
        </p>
      )}
      {cleared && (
        <p className="success">
          Moved {describe(cleared)} from {cleared.folder} to Drive's trash.
        </p>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  )
}
