import { useState } from 'react'
import { api, type BuildingBlock } from '../api'
import { useLoad } from '../live/useLoad'
import { PresetPicker } from '../newset/PresetPicker'
import { presetRequeue } from '../prompt/presets'

const QUEUED_FLASH_MS = 1500

interface Props {
  setId: number
  prompt: string
  resolution: string
  /** Used when the chosen preset has no image count. */
  fallbackCount: number
  className?: string
  onQueued: () => void
  onError: (message: string) => void
}

/** Queues more images of the set right away, with the chosen preset applied to the given prompt. */
export function RequeueWithPreset({ setId, prompt, resolution, fallbackCount, className, onQueued, onError }: Props) {
  const [picking, setPicking] = useState(false)
  const [queued, setQueued] = useState(false)
  const blocks = useLoad(api.buildingBlocks)
  const presets = blocks.data?.filter((block) => block.kind === 'Preset') ?? []

  async function requeue(preset: BuildingBlock) {
    setPicking(false)
    try {
      const request = presetRequeue(preset, prompt, resolution, fallbackCount)
      await api.moreImages(setId, request.count, request.prompt, request.resolution)
      setQueued(true)
      setTimeout(() => setQueued(false), QUEUED_FLASH_MS)
      onQueued()
    } catch (failure) {
      onError((failure as Error).message)
    }
  }

  return (
    <>
      <button type="button" className={className} onClick={() => setPicking(true)}>
        {queued ? 'Queued!' : 'Requeue with preset'}
      </button>
      {picking && (
        <PresetPicker presets={presets} onChoose={(preset) => void requeue(preset)} onClose={() => setPicking(false)} />
      )}
    </>
  )
}
