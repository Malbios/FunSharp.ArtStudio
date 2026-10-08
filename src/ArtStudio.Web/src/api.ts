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
  seed: string
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
  pickedCount: number
  deviantArtAuthor: string | null
  createdAt: string
  isDraft: boolean
  readyToPostAt: string | null
  archivedAt: string | null
  promptGeneration: PromptGeneration
}

export type PromptGenerationState = 'None' | 'Queued' | 'Running' | 'Done' | 'Failed'

export type PromptGenerationKind = 'Generate' | 'Modify'

export interface PromptGeneration {
  state: PromptGenerationState
  kind: PromptGenerationKind
  error: string | null
  truncated: boolean
  text: string | null
}

export interface ModifiedParagraph {
  index: number
  section: string
}

/** Images queued with the new prompt once a generation or modification is done. */
export interface ImagesToQueue {
  count: number
  resolution: string
}

export interface VisionSettings {
  hasApiKey: boolean
}

export interface SetDetail {
  id: number
  prompt: string
  resolution: string
  sourceKind: SourceKind
  sourceImageUrl: string | null
  deviantArtUrl: string | null
  deviantArtAuthor: string | null
  pickedImageIds: number[]
  createdAt: string
  isDraft: boolean
  readyToPostAt: string | null
  archivedAt: string | null
  promptGeneration: PromptGeneration
  images: ImageInfo[]
  jobs: Job[]
}

export interface DeviationImage {
  index: number
  imageUrl: string
  width: number
  height: number
}

export interface DeviationPreview {
  url: string
  deviationId: string
  author: string
  title: string | null
  images: DeviationImage[]
  unavailableImageCount: number
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

  sets: (stage: 'draft' | 'working' | 'ready' | 'archived') => request<SetSummary[]>('GET', `/api/sets?stage=${stage}`),
  set: (id: number) => request<SetDetail>('GET', `/api/sets/${id}`),
  createSet: (form: FormData) => request<{ id: number }>('POST', '/api/sets', form),
  moreImages: (setId: number, count: number, prompt?: string, resolution?: string) =>
    request<{ id: number }>('POST', `/api/sets/${setId}/more`, { count, prompt, resolution }),
  queueDraft: (setId: number, prompt: string, resolution: string, count: number) =>
    request<void>('POST', `/api/sets/${setId}/queue`, { prompt, resolution, count }),
  addDeviantArtDraft: (url: string) => request<{ id: number }>('POST', '/api/drafts/deviantart', { url }),
  generatePrompt: (setId: number, images?: ImagesToQueue) =>
    request<void>('POST', `/api/sets/${setId}/generate-prompt`, {
      imageCount: images?.count,
      resolution: images?.resolution,
    }),
  visionSettings: () => request<VisionSettings>('GET', '/api/vision'),
  saveVisionApiKey: (apiKey: string) => request<VisionSettings>('PUT', '/api/vision', { apiKey }),
  removeVisionApiKey: () => request<VisionSettings>('DELETE', '/api/vision'),
  modifyPrompt: (setId: number, prompt: string, instructions: string, paragraph?: ModifiedParagraph, images?: ImagesToQueue) =>
    request<void>('POST', `/api/sets/${setId}/modify-prompt`, {
      prompt,
      instructions,
      paragraphIndex: paragraph?.index,
      section: paragraph?.section,
      imageCount: images?.count,
      resolution: images?.resolution,
    }),
  pickImage: (setId: number, imageId: number) => request<void>('POST', `/api/sets/${setId}/picks`, { imageId }),
  unpickImage: (setId: number, imageId: number) => request<void>('DELETE', `/api/sets/${setId}/picks/${imageId}`),
  reorderPicks: (setId: number, imageIds: number[]) =>
    request<void>('PUT', `/api/sets/${setId}/picks`, { imageIds }),
  markReadyToPost: (setId: number) => request<void>('POST', `/api/sets/${setId}/ready`),
  moveBackToSets: (setId: number) => request<void>('DELETE', `/api/sets/${setId}/ready`),
  archiveSet: (setId: number) => request<void>('POST', `/api/sets/${setId}/archive`),
  restoreSet: (setId: number) => request<void>('DELETE', `/api/sets/${setId}/archive`),
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
