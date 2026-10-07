const SETS_OVERVIEW = '/sets'

export interface SetsOverviewState {
  setsOverview: string
}

/** Where the Sets overview was (including its page) when the set was opened from it; the first page otherwise. */
export function setsOverviewFrom(locationState: unknown): string {
  const overview = (locationState as Partial<SetsOverviewState> | null)?.setsOverview
  return typeof overview === 'string' ? overview : SETS_OVERVIEW
}
