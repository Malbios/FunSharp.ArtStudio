import type { DeviationImage, DeviationPreview } from '../api'

export type ImageSource =
  | { kind: 'None' }
  | { kind: 'Upload' | 'Paste'; file: File; previewUrl: string; width: number; height: number }
  | { kind: 'DeviantArt'; preview: DeviationPreview; imageIndex: number }
  | { kind: 'BasedOn'; imageUrl: string | null; deviantArtAuthor: string | null }

export const NO_SOURCE: ImageSource = { kind: 'None' }

export function sourceDimensions(source: ImageSource): { width: number; height: number } | null {
  switch (source.kind) {
    case 'Upload':
    case 'Paste':
      return { width: source.width, height: source.height }
    case 'DeviantArt': {
      const image = chosenDeviationImage(source)
      return { width: image.width, height: image.height }
    }
    default:
      return null
  }
}

export function sourceImageUrl(source: ImageSource): string | null {
  switch (source.kind) {
    case 'Upload':
    case 'Paste':
      return source.previewUrl
    case 'DeviantArt':
      return chosenDeviationImage(source).imageUrl
    case 'BasedOn':
      return source.imageUrl
    default:
      return null
  }
}

export function loadImageFile(kind: 'Upload' | 'Paste', file: File): Promise<ImageSource> {
  const previewUrl = URL.createObjectURL(file)
  return new Promise((resolve, reject) => {
    const image = new Image()
    image.onload = () => resolve({ kind, file, previewUrl, width: image.naturalWidth, height: image.naturalHeight })
    image.onerror = () => {
      URL.revokeObjectURL(previewUrl)
      reject(new Error('That file could not be read as an image.'))
    }
    image.src = previewUrl
  })
}

export function imageFileFrom(data: DataTransfer | null): File | null {
  if (!data) return null
  return Array.from(data.files).find((file) => file.type.startsWith('image/')) ?? null
}

export function chosenDeviationImage(source: { preview: DeviationPreview; imageIndex: number }): DeviationImage {
  return source.preview.images.find((image) => image.index === source.imageIndex) ?? source.preview.images[0]
}
