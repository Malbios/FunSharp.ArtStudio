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
  /** The order the prompt's sections were sent in, e.g. "Style,Characters,Setting,Composition"; null for the normal order. */
  paragraphOrder: string | null
  createdAt: string
}

export type JobKind = 'Image' | 'Video'

export interface Job {
  id: number
  setId: number
  kind: JobKind
  prompt: string
  /** A video job's size label, e.g. "896 x 512" or "0.55 MP". */
  resolution: string
  paragraphOrder: string | null
  /** For a video job, the image it starts from. */
  sourceImageUrl: string | null
  requestedCount: number
  completedCount: number
  status: JobStatus
  error: string | null
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  videoSeconds: number | null
  videoTurbo: boolean
  hasEndFrame: boolean
  /** How long one video of this job is expected to take, from earlier videos; null for image jobs. */
  estimatedSecondsPerVideo: number | null
  /** When ComfyUI was handed the video it renders now. */
  currentRunStartedAt: string | null
}

export interface VideoInfo {
  id: number
  setId: number
  jobId: number
  url: string
  seed: string
  prompt: string
  resolution: string
  seconds: number
  megapixels: number
  turbo: boolean
  startImageUrl: string
  endImageUrl: string | null
  /** Keys from api.videoFrames, for making the video again; null when the frame is no longer in the set. */
  startFrameKey: string | null
  endFrameKey: string | null
  picked: boolean
  createdAt: string
}

/** A frame a video of the set can start or end on: a generated image, the uploaded image or an earlier end frame. */
export interface VideoFrame {
  key: string
  url: string
  label: string
}

export interface VideoResolutionPreset {
  /** The size a 16:9 start image gets, e.g. "896 x 512". */
  name: string
  megapixels: number
  note: string | null
}

export interface VideoOptions {
  promptTemplate: string
  durationPresets: number[]
  resolutionPresets: VideoResolutionPreset[]
  defaultSeconds: number
  defaultResolution: string
  maxSeconds: number
  maxMegapixels: number
  maxCount: number
}

export interface VideoJobSettings {
  prompt: string
  seconds: number
  megapixels: number
  turbo: boolean
  count: number
  queueFirst: boolean
}

export type QueueMove = 'Top' | 'Up' | 'Down' | 'Bottom'

export interface QueueState {
  imagesPaused: boolean
  videosPaused: boolean
  /** The kind ComfyUI ran last, whose model is still loaded; null after a restart. */
  loadedKind: JobKind | null
  /** Whether images and videos render on the same ComfyUI server, so one kind waits for the other. */
  sharedServer: boolean
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
  videoCount: number
  deviantArtAuthor: string | null
  createdAt: string
  isDraft: boolean
  readyToPostAt: string | null
  archivedAt: string | null
  promptGeneration: PromptGeneration
  stashed: boolean
  publishedUrl: string | null
  publishedAt: string | null
  /** Has a title and tags beyond the default ones. */
  hasTitleAndTags: boolean
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
  paragraphOrder: string
}

export interface VisionSettings {
  hasApiKey: boolean
}

export interface SetDetail {
  id: number
  prompt: string
  resolution: string
  /** The order of the latest run, which "More images" reuses. */
  paragraphOrder: string | null
  sourceKind: SourceKind
  sourceImageUrl: string | null
  deviantArtUrl: string | null
  deviantArtAuthor: string | null
  /** Null for sets made before titles were kept; api.deviantArtTitle fetches those once. */
  deviantArtTitle: string | null
  pickedImageIds: number[]
  createdAt: string
  isDraft: boolean
  draftImageCount: number
  readyToPostAt: string | null
  archivedAt: string | null
  promptGeneration: PromptGeneration
  images: ImageInfo[]
  videos: VideoInfo[]
  jobs: Job[]
  post: PostDetails
}

export interface PostDetails {
  title: string | null
  description: string | null
  tags: string | null
  /** A string, as DeviantArt's ids are too large for JavaScript numbers. */
  stashItemId: string | null
  stashedAt: string | null
  publishedUrl: string | null
  publishedAt: string | null
  /** A title and tags are being suggested in the background. */
  suggesting: boolean
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
  /** Null when videos render on the image server. */
  videoComfyServerUrl: string | null
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

export interface OutputFolderContents {
  folder: string
  fileCount: number
  totalBytes: number
}

export interface GoogleApp {
  clientId: string | null
  hasClientSecret: boolean
  connected: boolean
  account: string | null
  connectedAt: string | null
  outputFolder: string
  redirectUri: string
}

export interface Gallery {
  folderId: string
  name: string
  mature: boolean
  lastUsed: boolean
}

export type MatureLevel = 'moderate' | 'strict'

export type BuildingBlockKind = 'Text' | 'Preset' | 'Bundle'

export interface BuildingBlock {
  id: number
  kind: BuildingBlockKind
  label: string
  text: string
  artStyle: string | null
  resolution: string | null
  imageCount: number | null
  paragraphOrder: string | null
  sortOrder: number
  imageUrl: string | null
  /** A bundle's presets in order. */
  presetIds: number[]
}

/** What a preset changes on the page; null leaves that part as it is. */
export interface PresetFields {
  label: string
  artStyle: string | null
  resolution: string | null
  imageCount: number | null
  paragraphOrder: string | null
}

export interface BlockedArtist {
  id: number
  username: string
  createdAt: string
}

export interface DeviantArtTotals {
  published: number
  tracked: number
  views: number
  favourites: number
  comments: number
  downloads: number
  viewsLast7Days: number
  favouritesLast7Days: number
}

/** The numbers are null until the deviation's stats were first fetched. */
export interface DeviationStats {
  setId: number
  title: string | null
  url: string
  previewImageUrl: string | null
  publishedAt: string
  daysOnline: number
  preset: string | null
  views: number | null
  favourites: number | null
  comments: number | null
  downloads: number | null
  favouritesPer100Views: number | null
  viewsPerDay: number | null
  viewsLast7Days: number | null
  favouritesLast7Days: number | null
}

export interface DeviantArtStats {
  connected: boolean
  fetchedAt: string | null
  stale: boolean
  historySince: string | null
  totals: DeviantArtTotals
  deviations: DeviationStats[]
  daily: { day: string; views: number; favourites: number }[]
}

export interface PresetStats {
  presetId: number | null
  name: string
  images: number
  picks: number
  pickRate: number | null
  published: number
  averageViews: number | null
  averageFavourites: number | null
}

export interface PipelineStats {
  stages: { drafts: number; working: number; post: number; stashed: number; published: number; archived: number }
  images: number
  imagesLast7Days: number
  imagesLast30Days: number
  imagesPerDay: { day: string; count: number }[]
  pickRate: number | null
  secondsPerImage: number | null
  jobsCompletedLast30Days: number
  jobsFailedLast30Days: number
  medianDaysToReady: number | null
  medianDaysReadyToPublished: number | null
}

export interface InspirationStats {
  sources: { source: SourceKind; sets: number }[]
  topArtists: { username: string; sets: number; published: number; averageFavourites: number | null }[]
}

export interface Stats {
  deviantArt: DeviantArtStats
  presets: PresetStats[]
  pipeline: PipelineStats
  inspiration: InspirationStats
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

  sets: (stage: 'draft' | 'working' | 'ready' | 'published' | 'archived', presetId?: number) =>
    request<SetSummary[]>('GET', `/api/sets?stage=${stage}${presetId ? `&preset=${presetId}` : ''}`),
  set: (id: number) => request<SetDetail>('GET', `/api/sets/${id}`),
  createSet: (form: FormData) => request<{ id: number }>('POST', '/api/sets', form),
  /** Without a paragraph order, the set's latest one is used. */
  moreImages: (
    setId: number,
    count: number,
    prompt?: string,
    resolution?: string,
    queueFirst?: boolean,
    paragraphOrder?: string,
  ) => request<{ id: number }>('POST', `/api/sets/${setId}/more`, { count, prompt, resolution, queueFirst, paragraphOrder }),
  videoOptions: () => request<VideoOptions>('GET', '/api/videos/options'),
  videoFrames: (setId: number) => request<VideoFrame[]>('GET', `/api/sets/${setId}/video-frames`),
  makeVideo: (setId: number, startFrame: string, endFrame: string | null, settings: VideoJobSettings) =>
    request<{ id: number }>('POST', `/api/sets/${setId}/videos`, { startFrame, endFrame, ...settings }),
  saveDraftSettings: (setId: number, resolution: string, imageCount: number) =>
    request<void>('PUT', `/api/sets/${setId}/draft-settings`, { resolution, imageCount }),
  queueDraft: (
    setId: number,
    prompt: string,
    resolution: string,
    count: number,
    queueFirst: boolean,
    paragraphOrder: string,
  ) => request<void>('POST', `/api/sets/${setId}/queue`, { prompt, resolution, count, queueFirst, paragraphOrder }),
  addDeviantArtDraft: (url: string) => request<{ id: number }>('POST', '/api/drafts/deviantart', { url }),
  retryFailedDraftPrompts: () => request<{ retried: number }>('POST', '/api/drafts/retry-failed-prompts'),
  generatePrompt: (setId: number, images?: ImagesToQueue) =>
    request<void>('POST', `/api/sets/${setId}/generate-prompt`, {
      imageCount: images?.count,
      resolution: images?.resolution,
      paragraphOrder: images?.paragraphOrder,
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
      paragraphOrder: images?.paragraphOrder,
    }),
  pickImage: (setId: number, imageId: number) => request<void>('POST', `/api/sets/${setId}/picks`, { imageId }),
  unpickImage: (setId: number, imageId: number) => request<void>('DELETE', `/api/sets/${setId}/picks/${imageId}`),
  pickVideo: (videoId: number, picked: boolean) => request<void>('PUT', `/api/videos/${videoId}/pick`, { picked }),
  reorderPicks: (setId: number, imageIds: number[]) =>
    request<void>('PUT', `/api/sets/${setId}/picks`, { imageIds }),
  markReadyToPost: (setId: number) => request<void>('POST', `/api/sets/${setId}/ready`),
  moveBackToSets: (setId: number) => request<void>('DELETE', `/api/sets/${setId}/ready`),
  archiveSet: (setId: number) => request<void>('POST', `/api/sets/${setId}/archive`),
  restoreSet: (setId: number) => request<void>('DELETE', `/api/sets/${setId}/archive`),
  deleteSet: (setId: number) => request<void>('DELETE', `/api/sets/${setId}`),

  queue: () => request<QueueState>('GET', '/api/queue'),
  /** Without a kind, pauses both the image and the video jobs. */
  pauseQueue: (kind?: JobKind) => request<void>('POST', `/api/queue/pause${kind ? `?kind=${kind}` : ''}`),
  resumeQueue: (kind?: JobKind) => request<void>('POST', `/api/queue/resume${kind ? `?kind=${kind}` : ''}`),
  cancelJob: (jobId: number) => request<void>('POST', `/api/jobs/${jobId}/cancel`),
  retryJob: (jobId: number) => request<void>('POST', `/api/jobs/${jobId}/retry`),
  moveJob: (jobId: number, move: QueueMove) => request<void>('POST', `/api/jobs/${jobId}/move`, { move }),

  previewDeviation: (url: string) => request<DeviationPreview>('POST', '/api/deviantart/preview', { url }),
  blockedArtists: () => request<BlockedArtist[]>('GET', '/api/blocked-artists'),
  blockArtist: (username: string) => request<BlockedArtist>('POST', '/api/blocked-artists', { username }),
  unblockArtist: (id: number) => request<void>('DELETE', `/api/blocked-artists/${id}`),

  deviantArtApp: () => request<DeviantArtApp>('GET', '/api/deviantart/app'),
  saveDeviantArtApp: (clientId: string, clientSecret: string) =>
    request<DeviantArtApp>('PUT', '/api/deviantart/app', { clientId, clientSecret }),
  disconnectDeviantArt: () => request<void>('POST', '/api/deviantart/disconnect'),
  deviantArtLoginUrl: '/api/deviantart/login',
  googleApp: () => request<GoogleApp>('GET', '/api/google/app'),
  saveGoogleApp: (clientId: string, clientSecret: string, outputFolder: string) =>
    request<GoogleApp>('PUT', '/api/google/app', { clientId, clientSecret, outputFolder }),
  disconnectGoogle: () => request<void>('POST', '/api/google/disconnect'),
  googleLoginUrl: '/api/google/login',
  comfyOutputFolder: () => request<OutputFolderContents>('GET', '/api/google/output-folder'),
  clearComfyOutputFolder: () => request<OutputFolderContents>('POST', '/api/google/output-folder/clear'),
  galleries: () => request<Gallery[]>('GET', '/api/deviantart/galleries'),
  saveMatureGalleries: (folderIds: string[]) =>
    request<Gallery[]>('PUT', '/api/deviantart/mature-galleries', { folderIds }),
  deviantArtTitle: (setId: number) =>
    request<{ title: string | null }>('POST', `/api/sets/${setId}/deviantart-title`),
  suggestTitle: (setId: number) => request<{ title: string }>('POST', `/api/sets/${setId}/post/suggest-title`),
  suggestTags: (setId: number) => request<{ tags: string[] }>('POST', `/api/sets/${setId}/post/suggest-tags`),
  stash: (setId: number, title: string, description: string, tags: string) =>
    request<void>('POST', `/api/sets/${setId}/post/stash`, { title, description, tags }),
  publish: (setId: number, galleryId: string, isMature: boolean, matureLevel: MatureLevel | null) =>
    request<void>('POST', `/api/sets/${setId}/post/publish`, { galleryId, isMature, matureLevel }),
  forgetStash: (setId: number) => request<void>('POST', `/api/sets/${setId}/post/forget-stash`),
  openExtraPicks: (setId: number) => request<{ folder: string }>('POST', `/api/sets/${setId}/post/extra-picks`),
  closeExtraPicks: (setId: number) => request<void>('POST', `/api/sets/${setId}/post/extra-picks/close`),

  settings: () => request<Settings>('GET', '/api/settings'),
  saveSettings: (settings: Settings) => request<Settings>('PUT', '/api/settings', settings),

  buildingBlocks: () => request<BuildingBlock[]>('GET', '/api/building-blocks'),
  addBuildingBlock: (label: string, text: string) =>
    request<BuildingBlock>('POST', '/api/building-blocks', { label, text }),
  updateBuildingBlock: (id: number, label: string, text: string) =>
    request<BuildingBlock>('PUT', `/api/building-blocks/${id}`, { label, text }),
  addPreset: (preset: PresetFields) =>
    request<BuildingBlock>('POST', '/api/building-blocks', { ...preset, kind: 'Preset' }),
  updatePreset: (id: number, preset: PresetFields) =>
    request<BuildingBlock>('PUT', `/api/building-blocks/${id}`, { ...preset, kind: 'Preset' }),
  setPresetImage: (id: number, image: Blob) => {
    const form = new FormData()
    form.append('image', image)
    return request<BuildingBlock>('PUT', `/api/building-blocks/${id}/image`, form)
  },
  removePresetImage: (id: number) => request<BuildingBlock>('DELETE', `/api/building-blocks/${id}/image`),
  addBundle: (label: string, presetIds: number[]) =>
    request<BuildingBlock>('POST', '/api/building-blocks', { label, presetIds, kind: 'Bundle' }),
  updateBundle: (id: number, label: string, presetIds: number[]) =>
    request<BuildingBlock>('PUT', `/api/building-blocks/${id}`, { label, presetIds, kind: 'Bundle' }),
  deleteBuildingBlock: (id: number) => request<void>('DELETE', `/api/building-blocks/${id}`),
  reorderBuildingBlocks: (ids: number[]) => request<void>('POST', '/api/building-blocks/reorder', { ids }),

  stats: () => request<Stats>('GET', '/api/stats'),
  refreshDeviantArtStats: (force: boolean) =>
    request<{ error: string | null }>('POST', '/api/stats/deviantart/refresh', { force }),
}
