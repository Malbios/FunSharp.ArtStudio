import { useEffect, useState, type RefObject } from 'react'

/** The element's current content width, following resizes; `fallback` until it is first measured. */
export function useElementWidth(ref: RefObject<HTMLElement | null>, fallback: number): number {
  const [width, setWidth] = useState(fallback)

  useEffect(() => {
    const element = ref.current
    if (!element) return
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width))
    observer.observe(element)
    return () => observer.disconnect()
  }, [ref])

  return width
}
