import { useState, type DragEvent } from 'react'

/** Drag-and-drop reordering of a list; `onMove` gets the dragged item's index and the index it was dropped on. */
export function useDragReorder(onMove: (from: number, to: number) => void) {
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [dropIndex, setDropIndex] = useState<number | null>(null)

  function reset() {
    setDragIndex(null)
    setDropIndex(null)
  }

  function itemProps(index: number) {
    return {
      draggable: true,
      onDragStart: () => setDragIndex(index),
      onDragEnd: reset,
      onDragOver: (event: DragEvent) => {
        event.preventDefault()
        setDropIndex(index)
      },
      onDrop: (event: DragEvent) => {
        event.preventDefault()
        if (dragIndex !== null && dragIndex !== index) onMove(dragIndex, index)
        reset()
      },
    }
  }

  function itemClass(index: number): string {
    return [dragIndex === index ? 'dragging' : '', dropIndex === index && dragIndex !== index ? 'drop-target' : '']
      .filter(Boolean)
      .join(' ')
  }

  return { itemProps, itemClass }
}

export function movedItem<T>(items: T[], from: number, to: number): T[] {
  const result = [...items]
  const [item] = result.splice(from, 1)
  result.splice(to, 0, item)
  return result
}
