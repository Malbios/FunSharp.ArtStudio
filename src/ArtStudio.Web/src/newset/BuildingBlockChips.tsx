import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type BuildingBlock } from '../api'
import { useLoad } from '../live/useLoad'
import { describePreset } from '../prompt/presets'

const FLASH_MS = 1200

interface Flash {
  id: number
  text: string
}

export function BuildingBlockChips({ onApplyPreset }: { onApplyPreset: (preset: BuildingBlock) => void }) {
  const blocks = useLoad(api.buildingBlocks)
  const [flash, setFlash] = useState<Flash>()
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
    flashChip(preset.id, 'Applied!')
  }

  const chip = (block: BuildingBlock, title: string | undefined, onClick: () => void) => (
    <button
      key={block.id}
      type="button"
      className={flash?.id === block.id ? 'chip copied' : 'chip'}
      title={title}
      onClick={onClick}
    >
      {flash?.id === block.id ? flash.text : block.label}
    </button>
  )

  const presetChip = (preset: BuildingBlock) => (
    <span key={preset.id} className="preset-chip">
      {chip(preset, undefined, () => apply(preset))}
      <span className="preset-preview" role="tooltip">
        {preset.imageUrl && <img src={preset.imageUrl} alt="" />}
        {describePreset(preset)}
      </span>
    </span>
  )

  return (
    <aside className="building-blocks">
      {presets.length > 0 && (
        <>
          <h3>Presets</h3>
          <div className="chips">{presets.map(presetChip)}</div>
        </>
      )}
      <h3>Building blocks</h3>
      {blocks.error && <p className="error">{blocks.error}</p>}
      {textBlocks?.length === 0 && <p className="hint">No building blocks yet.</p>}
      <div className="chips">{textBlocks?.map((block) => chip(block, block.text, () => void copy(block)))}</div>
      <Link to="/settings#building-blocks" className="hint">
        Manage building blocks
      </Link>
    </aside>
  )
}
