/**
 * 图片素材。管理后台和素材接口（T-014）完成前，上传的图片存在浏览器本地；模板里写 `asset:素材ID`。
 */
import { reactive, ref } from 'vue'

export interface Asset {
  id: string
  name: string
  /** data URL。 */
  src: string
  builtin: boolean
}

const STORAGE_KEY = 'label-designer.assets'
const MAX_BYTES = 1024 * 1024
const svg = (markup: string) => 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(markup)

const BUILTIN: Asset[] = [
  {
    id: 'logo',
    name: '公司 Logo（示例）',
    builtin: true,
    src: svg('<svg xmlns="http://www.w3.org/2000/svg" width="240" height="80" viewBox="0 0 240 80"><rect x="4" y="12" width="56" height="56" rx="12" fill="#111"/><path d="M18 54l14-28 14 28z" fill="#fff"/><text x="74" y="52" font-family="Arial, Helvetica, sans-serif" font-weight="700" font-size="34" fill="#111">XX TECH</text></svg>'),
  },
  {
    id: 'qcpass',
    name: 'QC PASS 印章',
    builtin: true,
    src: svg('<svg xmlns="http://www.w3.org/2000/svg" width="160" height="160" viewBox="0 0 80 80"><circle cx="40" cy="40" r="36" fill="none" stroke="#111" stroke-width="4"/><circle cx="40" cy="40" r="29" fill="none" stroke="#111" stroke-width="1.5"/><text x="40" y="39" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-weight="700" font-size="17" fill="#111">QC</text><text x="40" y="56" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-weight="700" font-size="13" fill="#111">PASS</text></svg>'),
  },
]

function loadUploaded(): Asset[] {
  try {
    const list = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]') as Asset[]
    return Array.isArray(list) ? list.filter((asset) => asset.id && asset.src) : []
  } catch {
    return []
  }
}

export const assets = reactive<Asset[]>([...BUILTIN, ...loadUploaded()])

/** 素材变化（上传）时加 1，画布据此重画引用素材的图片元素。 */
export const assetVersion = ref(0)

export function assetSource(id: string): string | null {
  return assets.find((asset) => asset.id === id)?.src ?? null
}

/** 上传图片素材：PNG、JPG、SVG，不超过 1 MB。 */
export function uploadAsset(file: File): Promise<Asset> {
  return new Promise((resolve, reject) => {
    if (!/^image\/(png|jpeg|svg\+xml)$/.test(file.type)) {
      reject(new Error('只支持 PNG、JPG、SVG 图片'))
      return
    }

    if (file.size > MAX_BYTES) {
      reject(new Error('图片超过 1 MB，请压缩后再上传'))
      return
    }

    const reader = new FileReader()
    reader.onload = () => {
      const asset: Asset = { id: 'a' + Math.random().toString(36).slice(2, 9), name: file.name.replace(/\.\w+$/, ''), src: String(reader.result), builtin: false }
      assets.push(asset)
      assetVersion.value++
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(assets.filter((item) => !item.builtin)))
      } catch {
        // 本地存储满了或不可用：这次会话里仍然能用。
      }

      resolve(asset)
    }
    reader.onerror = () => reject(new Error('读取图片失败'))
    reader.readAsDataURL(file)
  })
}
