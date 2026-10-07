import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { useLoad } from '../live/useLoad'

const COPIED_FLASH_MS = 1200

export function BuildingBlockChips() {
  const blocks = useLoad(api.buildingBlocks)
  const [copiedId, setCopiedId] = useState<number>()

  async function copy(id: number, text: string) {
    await navigator.clipboard.writeText(text)
    setCopiedId(id)
    setTimeout(() => setCopiedId((current) => (current === id ? undefined : current)), COPIED_FLASH_MS)
  }

  return (
    <aside className="building-blocks">
      <h3>Building blocks</h3>
      {blocks.error && <p className="error">{blocks.error}</p>}
      {blocks.data?.length === 0 && <p className="hint">No building blocks yet.</p>}
      <div className="chips">
        {blocks.data?.map((block) => (
          <button
            key={block.id}
            type="button"
            className={copiedId === block.id ? 'chip copied' : 'chip'}
            title={block.text}
            onClick={() => void copy(block.id, block.text)}
          >
            {copiedId === block.id ? 'Copied!' : block.label}
          </button>
        ))}
      </div>
      <Link to="/settings#building-blocks" className="hint">
        Manage building blocks
      </Link>
    </aside>
  )
}
