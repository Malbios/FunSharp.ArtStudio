import { useCallback, useEffect, useRef, useState } from 'react'

export interface Loaded<T> {
  data: T | undefined
  error: string | undefined
  reload: () => void
}

/** Loads data on mount and whenever `key` changes; `reload` refetches on demand. */
export function useLoad<T>(load: () => Promise<T>, key: unknown = null): Loaded<T> {
  const [data, setData] = useState<T>()
  const [error, setError] = useState<string>()
  const loadRef = useRef(load)
  const requestCounter = useRef(0)

  useEffect(() => {
    loadRef.current = load
  })

  const reload = useCallback(() => {
    const requestId = ++requestCounter.current
    loadRef.current().then(
      (result) => {
        if (requestId !== requestCounter.current) return
        setData(result)
        setError(undefined)
      },
      (failure: Error) => {
        if (requestId !== requestCounter.current) return
        setError(failure.message)
      },
    )
  }, [])

  useEffect(() => reload(), [reload, key])

  return { data, error, reload }
}
