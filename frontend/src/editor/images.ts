/**
 * 图片加载缓存。同一个地址只加载一次；需要黑白化时另存一份处理后的画布。
 * 图片还没加载好时返回 null，加载完成后调用回调让画布重画。
 */
/**
 * 缓存里的一项。
 */
interface Entry {
  /**
   * 加载结束（成功或失败）。
   */
  ready: boolean
  /**
   * 加载好的图片（黑白化时是处理后的画布）；失败时为 null。
   */
  image: CanvasImageSource | null
  /**
   * 加载结束时要通知的回调。
   */
  waiters: Array<() => void>
}

/**
 * 图片缓存，key 是地址（黑白化的前面加 mono|）。
 */
const cache = new Map<string, Entry>()

/**
 * 取图片：加载好了直接返回，否则开始加载（或排队等待），加载结束时调用 onReady。
 * @param src 图片地址。
 * @param mono 是否黑白化。
 * @param onReady 还没加载好时，加载结束后调用。
 * @returns 图片；还没加载好或加载失败时为 null。
 */
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

/**
 * 图片的原始尺寸。
 * @param image 图片或画布。
 * @returns 宽高，像素；认不出时为 1×1。
 */
export function imageSize(image: CanvasImageSource): { width: number; height: number } {
  if (image instanceof HTMLImageElement) return { width: image.naturalWidth || image.width, height: image.naturalHeight || image.height }
  if (image instanceof HTMLCanvasElement) return { width: image.width, height: image.height }
  return { width: 1, height: 1 }
}

/**
 * 按热敏打印的效果转黑白：亮度阈值 150，透明处保持透明。
 * @param image 加载好的图片。
 * @returns 黑白画布（宽度不超过 1600 像素）。
 */
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
