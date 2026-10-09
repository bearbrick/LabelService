/**
 * 图片加载缓存。同一个地址只加载一次；需要黑白化时另存一份处理后的画布。
 * 图片还没加载好时返回 null，加载完成后调用回调让画布重画。
 */
interface Entry {
  ready: boolean
  image: CanvasImageSource | null
  waiters: Array<() => void>
}

const cache = new Map<string, Entry>()

export function getImage(src: string, mono: boolean, onReady: () => void): CanvasImageSource | null {
  const key = (mono ? 'mono|' : '') + src
  const hit = cache.get(key)
  if (hit?.ready) return hit.image
  if (hit) {
    hit.waiters.push(onReady)
    return null
  }

  const entry: Entry = { ready: false, image: null, waiters: [onReady] }
  cache.set(key, entry)
  const image = new Image()
  image.onload = () => {
    entry.image = mono ? toMonochrome(image) : image
    entry.ready = true
    entry.waiters.splice(0).forEach((callback) => callback())
  }
  image.onerror = () => {
    entry.ready = true
    entry.waiters.splice(0).forEach((callback) => callback())
  }
  image.src = src
  return null
}

/** 图片的原始尺寸。 */
export function imageSize(image: CanvasImageSource): { width: number; height: number } {
  if (image instanceof HTMLImageElement) return { width: image.naturalWidth || image.width, height: image.naturalHeight || image.height }
  if (image instanceof HTMLCanvasElement) return { width: image.width, height: image.height }
  return { width: 1, height: 1 }
}

/** 按热敏打印的效果转黑白：亮度阈值 150，透明处保持透明。 */
function toMonochrome(image: HTMLImageElement): HTMLCanvasElement {
  const width = Math.min(image.naturalWidth || 300, 1600)
  const height = Math.round((width * (image.naturalHeight || 150)) / (image.naturalWidth || 300))
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const context = canvas.getContext('2d')!
  context.drawImage(image, 0, 0, width, height)
  const data = context.getImageData(0, 0, width, height)
  const pixels = data.data
  for (let i = 0; i < pixels.length; i += 4) {
    if (pixels[i + 3] < 20) continue
    const alpha = pixels[i + 3] / 255
    const luminance = (0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2]) * alpha + 255 * (1 - alpha)
    const black = luminance < 150
    pixels[i] = pixels[i + 1] = pixels[i + 2] = black ? 0 : 255
    pixels[i + 3] = black ? 255 : 0
  }

  context.putImageData(data, 0, 0)
  return canvas
}
