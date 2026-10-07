import { useState, type FormEvent } from 'react'
import { api, type BuildingBlock } from '../api'
import { useLoad } from '../live/useLoad'

export function BuildingBlocksEditor() {
  const blocks = useLoad(api.buildingBlocks)
  const [newLabel, setNewLabel] = useState('')
  const [newText, setNewText] = useState('')
  const [error, setError] = useState<string>()

  async function run(action: () => Promise<unknown>) {
    setError(undefined)
    try {
      await action()
      blocks.reload()
    } catch (failure) {
      setError((failure as Error).message)
    }
  }

  async function add(event: FormEvent) {
    event.preventDefault()
    await run(async () => {
      await api.addBuildingBlock(newLabel, newText)
      setNewLabel('')
      setNewText('')
    })
  }

  function move(index: number, offset: number) {
    const ids = (blocks.data ?? []).map((block) => block.id)
    const [moved] = ids.splice(index, 1)
    ids.splice(index + offset, 0, moved)
    void run(() => api.reorderBuildingBlocks(ids))
  }

  const list = blocks.data ?? []

  return (
    <section className="panel" id="building-blocks">
      <h3>Building blocks</h3>
      <p className="hint">Shown next to the prompt box. Clicking one copies its text to the clipboard.</p>
      <div className="editable-list">
        {list.map((block, index) => (
          <BuildingBlockRow
            key={`${block.id}-${block.label}-${block.text}`}
            block={block}
            canMoveUp={index > 0}
            canMoveDown={index < list.length - 1}
            onSave={(label, text) => void run(() => api.updateBuildingBlock(block.id, label, text))}
            onDelete={() => void run(() => api.deleteBuildingBlock(block.id))}
            onMove={(offset) => move(index, offset)}
          />
        ))}
      </div>
      <form className="editable-row" onSubmit={(e) => void add(e)}>
        <input placeholder="Label (optional)" value={newLabel} onChange={(e) => setNewLabel(e.target.value)} />
        <textarea rows={2} placeholder="Text to copy" value={newText} onChange={(e) => setNewText(e.target.value)} />
        <button type="submit" className="primary" disabled={!newText.trim()}>
          Add
        </button>
      </form>
      {(error ?? blocks.error) && <p className="error">{error ?? blocks.error}</p>}
    </section>
  )
}

interface RowProps {
  block: BuildingBlock
  canMoveUp: boolean
  canMoveDown: boolean
  onSave: (label: string, text: string) => void
  onDelete: () => void
  onMove: (offset: number) => void
}

function BuildingBlockRow({ block, canMoveUp, canMoveDown, onSave, onDelete, onMove }: RowProps) {
  const [label, setLabel] = useState(block.label)
  const [text, setText] = useState(block.text)
  const changed = label !== block.label || text !== block.text

  return (
    <div className="editable-row">
      <input value={label} onChange={(e) => setLabel(e.target.value)} />
      <textarea rows={2} value={text} onChange={(e) => setText(e.target.value)} />
      <div className="row-actions">
        <button type="button" disabled={!canMoveUp} onClick={() => onMove(-1)} title="Move up">
          ↑
        </button>
        <button type="button" disabled={!canMoveDown} onClick={() => onMove(1)} title="Move down">
          ↓
        </button>
        <button type="button" className="primary" disabled={!changed} onClick={() => onSave(label, text)}>
          Save
        </button>
        <button type="button" className="danger" onClick={onDelete}>
          Delete
        </button>
      </div>
    </div>
  )
}
