/**
 * 图片素材：来自服务端素材库（GET /api/admin/assets），模板里写 `asset:key`。
 * 设计器显示用的文件按需下载成 blob URL；上传走 POST /api/admin/assets（需要 res.manage）。
 */
import { reactive, ref } from 'vue'
import * as adminApi from '../api/admin.ts'
import type { AssetInfo } from '../api/admin.ts'

/**
 * 素材列表的一项：服务端的信息加上下载好的文件地址。
 */
export interface Asset extends AssetInfo {
  /**
   * blob URL；还没下载完时为空字符串。
   */
  src: string
}

/**
 * 全部素材（内置的和上传的）。
 */
export const assets = reactive<Asset[]>([])

/**
 * 素材变化（列表刷新、文件下载完）时加 1，画布据此重画引用素材的图片元素。
 */
export const assetVersion = ref(0)

/**
 * 进行中的加载；失败后清空，下次重新请求。
 */
let loading: Promise<void> | null = null

/**
 * 读取素材列表并下载文件。重复调用时复用进行中的请求。
 * @param force 强制重新读取（上传后用）。
 * @returns 加载完成时结束。
 */
export function loadAssets(force = false): Promise<void> {
  if (loading && !force) return loading
  loading = (async () => {
    const list = await adminApi.listAssets()
    for (const old of assets) if (old.src && !list.some((item) => item.key === old.key)) URL.revokeObjectURL(old.src)
    const next = list.map((item) => ({ ...item, src: assets.find((old) => old.key === item.key)?.src ?? '' }))
    assets.splice(0, assets.length, ...next)
    assetVersion.value++
    await Promise.all(assets.filter((asset) => !asset.src).map(fetchFile))
    assetVersion.value++
  })()
  loading.catch(() => (loading = null))
  return loading
}

/**
 * 下载一个素材文件，转成 blob URL。
 * @param asset 素材（下载好后写入 src）。
 * @returns 下载结束时完成（失败也不抛）。
 */
async function fetchFile(asset: Asset): Promise<void> {
  try {
    const response = await adminApi.assetFile(asset.key)
    asset.src = URL.createObjectURL(await response.blob())
  } catch {
    // 单个文件下载失败时画布显示占位框，不影响其他素材。
  }
}

/**
 * 素材 key 对应的文件地址（画布显示用）。
 * @param key 素材 key。
 * @returns blob URL；不存在或还没下载完时为 null。
 */
export function assetSource(key: string): string | null {
  return assets.find((asset) => asset.key === key)?.src || null
}

/**
 * 上传图片素材：PNG、JPG、SVG，不超过 1 MB（服务端按内容再判断一次）。
 * @param file 图片文件。
 * @returns 新素材（已重新加载列表）。
 */
export async function uploadAsset(file: File): Promise<Asset> {
  if (!/^image\/(png|jpeg|svg\+xml)$/.test(file.type)) throw new Error('只支持 PNG、JPG、SVG 图片')
  if (file.size > 1024 * 1024) throw new Error('图片超过 1 MB，请压缩后再上传')
  const info = await adminApi.uploadAsset(file)
  await loadAssets(true)
  return assets.find((asset) => asset.key === info.key) ?? { ...info, src: '' }
}
