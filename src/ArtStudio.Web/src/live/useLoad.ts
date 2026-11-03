import { useCallback, useEffect, useRef, useState } from 'react'
import { coalesce } from './coalesce'

export interface Loaded<T> {
  data: T | undefined
  error: string | undefined
  reload: () => void
}

/** Loads data on mount and whenever `key` changes; `reload` refetches on demand, one request at a time. */
export function useLoad<T>(load: () => Promise<T>, key: unknown = null): Loaded<T> {
  const [data, setData] = useState<T>()
  const [error, setError] = useState<string>()
  const loadRef = useRef(load)
  const coalescedReload = useRef<() => void>(undefined)

  useEffect(() => {
    loadRef.current = load
  })

  const reload = useCallback(() => {
    coalescedReload.current ??= coalesce(async () => {
      try {
        setData(await loadRef.current())
        setError(undefined)
      } catch (failure) {
        setError((failure as Error).message)
      }
    })
    coalescedReload.current()
  }, [])

  useEffect(() => reload(), [reload, key])

  return { data, error, reload }
}
