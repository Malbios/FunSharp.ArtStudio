import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type BuildingBlock } from '../api'
import { useLoad } from '../live/useLoad'
import { PresetPicker } from './PresetPicker'

const FLASH_MS = 1200

interface Flash {
  id: number
  text: string
}

export function BuildingBlockChips({ onApplyPreset }: { onApplyPreset: (preset: BuildingBlock) => void }) {
  const blocks = useLoad(api.buildingBlocks)
  const [flash, setFlash] = useState<Flash>()
  const [pickingPreset, setPickingPreset] = useState(false)
  const [appliedPreset, setAppliedPreset] = useState<string>()
  const presets = blocks.data?.filter((block) => block.kind === 'Preset') ?? []
  const textBlocks = blocks.data?.filter((block) => block.kind === 'Text')

  function flashChip(id: number, text: string) {
    setFlash({ id, text })
    setTimeout(() => setFlash((current) => (current?.id === id ? undefined : current)), FLASH_MS)
  }

  async function copy(block: BuildingBlock) {
    await navigator.clipboard.writeText(block.text)
    flashChip(block.id, 'Copied!')
  }

  function apply(preset: BuildingBlock) {
    onApplyPreset(preset)
    setPickingPreset(false)
    setAppliedPreset(preset.label)
    setTimeout(() => setAppliedPreset((current) => (current === preset.label ? undefined : current)), FLASH_MS)
  }

  return (
    <aside className="building-blocks">
      {presets.length > 0 && (
        <button
          type="button"
          className={appliedPreset ? 'preset-button copied' : 'preset-button'}
          onClick={() => setPickingPreset(true)}
        >
          {appliedPreset ? `Applied: ${appliedPreset}` : `Presets (${presets.length})`}
        </button>
      )}
      {pickingPreset && <PresetPicker presets={presets} onChoose={apply} onClose={() => setPickingPreset(false)} />}
      <h3>Building blocks</h3>
      {blocks.error && <p className="error">{blocks.error}</p>}
      {textBlocks?.length === 0 && <p className="hint">No building blocks yet.</p>}
      <div className="chips">
        {textBlocks?.map((block) => (
          <button
            key={block.id}
            type="button"
            className={flash?.id === block.id ? 'chip copied' : 'chip'}
            title={block.text}
            onClick={() => void copy(block)}
          >
            {flash?.id === block.id ? flash.text : block.label}
          </button>
        ))}
      </div>
      <Link to="/settings#building-blocks" className="hint">
        Manage building blocks
      </Link>
    </aside>
  )
}
