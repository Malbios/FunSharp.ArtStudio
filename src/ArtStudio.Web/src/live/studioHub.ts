import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef } from 'react'

export type StudioEvent = 'JobUpdated' | 'ImageAdded' | 'JobCompleted' | 'QueueStateChanged' | 'QueueEmpty'

export interface JobEventPayload {
  jobId: number
  setId: number
  prompt?: string
}

type Listener = (event: StudioEvent | 'Reconnected', payload: unknown) => void

const ALL_EVENTS: StudioEvent[] = ['JobUpdated', 'ImageAdded', 'JobCompleted', 'QueueStateChanged', 'QueueEmpty']
const listeners = new Set<Listener>()

const connection = new HubConnectionBuilder()
  .withUrl('/hubs/studio')
  .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 3000 })
  .configureLogging(LogLevel.Warning)
  .build()

for (const event of ALL_EVENTS) {
  connection.on(event, (payload: unknown) => listeners.forEach((listener) => listener(event, payload)))
}
connection.onreconnected(() => listeners.forEach((listener) => listener('Reconnected', undefined)))

async function startConnection() {
  try {
    await connection.start()
  } catch {
    setTimeout(startConnection, 3000)
  }
}
void startConnection()

/** Calls the handler for the given events, and after a reconnect so views can catch up on missed changes. */
export function useStudioEvents(events: StudioEvent[], handler: (event: StudioEvent | 'Reconnected', payload: unknown) => void) {
  const handlerRef = useRef(handler)
  const eventKey = events.join(',')

  useEffect(() => {
    handlerRef.current = handler
  })

  useEffect(() => {
    const watched = new Set(eventKey.split(','))
    const listener: Listener = (event, payload) => {
      if (event === 'Reconnected' || watched.has(event)) handlerRef.current(event, payload)
    }
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  }, [eventKey])
}
