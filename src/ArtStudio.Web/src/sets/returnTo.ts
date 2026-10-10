import { useLocation } from 'react-router-dom'

const DEFAULT_RETURN = '/sets'
const SET_DETAIL_PATH = /^\/sets\/\d+$/

export interface ReturnState {
  returnTo: string
}

/** The page (including its page number and filter) a set was opened from; Sets otherwise. */
export function returnToFrom(locationState: unknown): string {
  const returnTo = (locationState as Partial<ReturnState> | null)?.returnTo
  return typeof returnTo === 'string' ? returnTo : DEFAULT_RETURN
}

/** State for links to a set, so leaving the set returns here; a set page passes on where it was opened from. */
export function useReturnState(): ReturnState {
  const location = useLocation()
  return {
    returnTo: SET_DETAIL_PATH.test(location.pathname)
      ? returnToFrom(location.state)
      : location.pathname + location.search,
  }
}
