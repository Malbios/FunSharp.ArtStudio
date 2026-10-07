export type SourceKind = 'None' | 'Upload' | 'Paste' | 'DeviantArt'
export type JobStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled'

export interface ResolutionPreset {
  name: string
  width: number
  height: number
  comfyLabel: string
}

export interface ImageInfo {
  id: number
  setId: number
  jobId: number
  url: string
  seed: number
  prompt: string
  resolution: string
  createdAt: string
}

export interface Job {
  id: number
  setId: number
  prompt: string
  resolution: string
  sourceImageUrl: string | null
  requestedCount: number
  completedCount: number
  status: JobStatus
  error: string | null
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
}

export interface QueueState {
  paused: boolean
  active: Job[]
  recent: Job[]
}

export interface SetSummary {
  id: number
  prompt: string
  resolution: string
  sourceKind: SourceKind
  sourceImageUrl: string | null
  previewImageUrl: string | null
  imageCount: number
  hasActiveJob: boolean
  hasFailedJob: boolean
  selectedImageId: number | null
  createdAt: string
}

export interface SetDetail {
  id: number
  prompt: string
  resolution: string
  sourceKind: SourceKind
  sourceImageUrl: string | null
  deviantArtUrl: string | null
  deviantArtAuthor: string | null
  selectedImageId: number | null
  createdAt: string
  images: ImageInfo[]
  jobs: Job[]
}

export interface DeviationPreview {
  url: string
  deviationId: string
  author: string
  title: string | null
  imageUrl: string
  width: number
  height: number
}

export interface Settings {
  comfyServerUrl: string
  outputDirectory: string
  notifyOnJobDone: boolean
  notifyOnQueueEmpty: boolean
}

export interface DeviantArtApp {
  clientId: string | null
  hasClientSecret: boolean
  connected: boolean
  username: string | null
  redirectUri: string
}

export interface BuildingBlock {
  id: number
  label: string
  text: string
  sortOrder: number
}

export interface BlockedArtist {
  id: number
  username: string
  createdAt: string
}

export class ApiError extends Error {
  readonly existingSetId: number | null

  constructor(message: string, existingSetId: number | null) {
    super(message)
    this.existingSetId = existingSetId
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const init: RequestInit = { method }
  if (body instanceof FormData) {
    init.body = body
  } else if (body !== undefined) {
    init.body = JSON.stringify(body)
    init.headers = { 'Content-Type': 'application/json' }
  }

  const response = await fetch(url, init)
  const text = await response.text()
  const json = text ? JSON.parse(text) : undefined
  if (!response.ok) {
    throw new ApiError(json?.error ?? `Request failed (${response.status})`, json?.existingSetId ?? null)
  }
  return json as T
}

export const api = {
  resolutions: () => request<ResolutionPreset[]>('GET', '/api/resolutions'),

  sets: () => request<SetSummary[]>('GET', '/api/sets'),
  set: (id: number) => request<SetDetail>('GET', `/api/sets/${id}`),
  createSet: (form: FormData) => request<{ id: number }>('POST', '/api/sets', form),
  moreImages: (setId: number, count: number, prompt?: string, resolution?: string) =>
    request<{ id: number }>('POST', `/api/sets/${setId}/more`, { count, prompt, resolution }),
  selectImage: (setId: number, imageId: number | null) =>
    request<void>('POST', `/api/sets/${setId}/select`, { imageId }),
  deleteSet: (setId: number) => request<void>('DELETE', `/api/sets/${setId}`),

  queue: () => request<QueueState>('GET', '/api/queue'),
  pauseQueue: () => request<void>('POST', '/api/queue/pause'),
  resumeQueue: () => request<void>('POST', '/api/queue/resume'),
  cancelJob: (jobId: number) => request<void>('POST', `/api/jobs/${jobId}/cancel`),
  retryJob: (jobId: number) => request<void>('POST', `/api/jobs/${jobId}/retry`),

  previewDeviation: (url: string) => request<DeviationPreview>('POST', '/api/deviantart/preview', { url }),
  blockedArtists: () => request<BlockedArtist[]>('GET', '/api/blocked-artists'),
  blockArtist: (username: string) => request<BlockedArtist>('POST', '/api/blocked-artists', { username }),
  unblockArtist: (id: number) => request<void>('DELETE', `/api/blocked-artists/${id}`),

  deviantArtApp: () => request<DeviantArtApp>('GET', '/api/deviantart/app'),
  saveDeviantArtApp: (clientId: string, clientSecret: string) =>
    request<DeviantArtApp>('PUT', '/api/deviantart/app', { clientId, clientSecret }),
  disconnectDeviantArt: () => request<void>('POST', '/api/deviantart/disconnect'),
  deviantArtLoginUrl: '/api/deviantart/login',

  settings: () => request<Settings>('GET', '/api/settings'),
  saveSettings: (settings: Settings) => request<Settings>('PUT', '/api/settings', settings),

  buildingBlocks: () => request<BuildingBlock[]>('GET', '/api/building-blocks'),
  addBuildingBlock: (label: string, text: string) =>
    request<BuildingBlock>('POST', '/api/building-blocks', { label, text }),
  updateBuildingBlock: (id: number, label: string, text: string) =>
    request<BuildingBlock>('PUT', `/api/building-blocks/${id}`, { label, text }),
  deleteBuildingBlock: (id: number) => request<void>('DELETE', `/api/building-blocks/${id}`),
  reorderBuildingBlocks: (ids: number[]) => request<void>('POST', '/api/building-blocks/reorder', { ids }),
}
