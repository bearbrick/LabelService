/** 实际 Chrome 分段按钮回归；仅访问显式指定的本地隔离服务和合成数据。 */
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import path from 'node:path'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE)
const base = process.env.LABEL_UI_URL
const backend = process.env.LABEL_UI_BACKEND
const password = process.env.LABEL_UI_PASSWORD
const output = process.env.LABEL_UI_EVIDENCE
const mode = process.argv[2] ?? 'after'
assert(new URL(base).hostname === '127.0.0.1' && new URL(backend).hostname === '127.0.0.1' && password && output)
await fs.mkdir(output, { recursive: true })
const results = [], checks = [], errors = []
let context, page, token
/** 向本次隔离服务读写合成模板，保留测试数据。 */
async function api(method, route, body) {
  const response = await fetch(backend + '/api/admin/' + route, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) })
  assert(response.ok, method + ' ' + route + ' ' + response.status)
  return response.status === 204 ? null : response.json()
}
/** 记录有用途的布局与交互断言。 */
function check(name, value) { checks.push({ name, passed: !!value }); assert(value, name) }
/** 等待实际路由渲染后再检查。 */
async function nav(route) { await page.evaluate(r => { location.hash = '#/' + r }, route); await page.waitForTimeout(250) }
/** 量出每个分段组边界、选项宽度和最后选项后的空白，并保存实际截图。 */
async function measure(name) {
  const groups = await page.locator('.seg').evaluateAll(elements => elements.filter(x => x.getBoundingClientRect().width > 0 && getComputedStyle(x).visibility !== 'hidden').map(x => {
    const r = x.getBoundingClientRect(), buttons = [...x.querySelectorAll('button')].map(b => { const q = b.getBoundingClientRect(); return { text: b.getAttribute('aria-label') || b.textContent, left: q.left, right: q.right, width: q.width, height: q.height } })
    return { label: x.getAttribute('aria-label'), left: r.left, right: r.right, width: r.width, gap: buttons.length ? r.right - parseFloat(getComputedStyle(x).borderRightWidth) - buttons.at(-1).right : 0, buttons }
  }))
  const dimensions = await page.evaluate(() => ({ width: innerWidth, height: innerHeight, dpr: devicePixelRatio, documentWidth: document.documentElement.scrollWidth }))
  check(name + ' 页面无横向溢出', dimensions.documentWidth <= dimensions.width + 1)
  check(name + ' 可见组有按钮且按钮高不少于 28px', groups.every(g => g.buttons.length && g.buttons.every(b => b.height >= 28)))
  if (mode === 'after') check(name + ' 末尾不留空白', groups.every(g => Math.abs(g.gap) <= 1.5))
  results.push({ name, groups, dimensions })
  await page.screenshot({ path: path.join(output, name + '.png') })
}
/** 点选分段组末尾和首项，确认真实 Vue 状态显示。 */
async function interact(selector, name) {
  const groups = page.locator(selector)
  for (let index = 0; index < await groups.count(); index++) {
    const group = groups.nth(index)
    if (!(await group.isVisible())) continue
    const buttons = group.locator('button')
    for (const position of [await buttons.count() - 1, 0]) {
      const button = buttons.nth(position)
      await button.click()
      check(name + ' 第' + index + '组选项' + position + '可切换', /\bon\b/.test(await button.getAttribute('class') ?? ''))
    }
  }
}
try {
  token = (await api('POST', 'login', { account: 'admin', password, remember: false })).token
  const businesses = await api('GET', 'businesses')
  const business = (Array.isArray(businesses) ? businesses : businesses.items).find(x => x.code === 'SAMPLE')
  const code = 'SEGMENT_REVIEW_' + Date.now()
  await api('POST', 'templates', { code, name: '分段按钮隔离回归', businessId: business.id, width: 100, height: 60, dpi: 300 })
  context = await chromium.launchPersistentContext(path.join(output, 'chrome-profile'), { channel: 'chrome', headless: true, viewport: { width: 1920, height: 1080 } })
  page = await context.newPage()
  page.on('pageerror', e => errors.push(String(e)))
  await page.goto(base, { waitUntil: 'networkidle' })
  await page.locator('#lg-account').fill('admin')
  await page.locator('#lg-pwd').fill(password)
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await page.locator('.topbar').waitFor()
  await nav('t/' + code + '/designer')
  await page.waitForFunction(c => window.__labelDesigner?.designer.template.code === c, code)
  await page.evaluate(() => { for (const type of ['text', 'barcode', 'qrcode', 'image', 'icon', 'line', 'rect']) window.__labelDesigner.addElement(type) })
  const settings = await context.newPage()
  await settings.goto('chrome://settings/appearance')
  const cases = mode === 'before' ? [{ width: 1920, height: 1080, zoom: 1 }] : [{ width: 1920, height: 1080, zoom: 1 }, { width: 1280, height: 720, zoom: 1 }, { width: 1101, height: 700, zoom: 1 }, { width: 1920, height: 1080, zoom: 1.25 }, { width: 1920, height: 1080, zoom: 1.5 }]
  for (const size of cases) {
    await page.setViewportSize({ width: size.width, height: size.height })
    await settings.locator('select#zoomLevel').selectOption(String(size.zoom))
    await page.reload({ waitUntil: 'networkidle' })
    await page.waitForFunction(c => window.__labelDesigner?.designer.template.code === c, code)
    const suffix = size.width + '-' + Math.round(size.zoom * 100)
    check(suffix + ' 原生缩放 DPR 正确', Math.abs(await page.evaluate(() => devicePixelRatio) - size.zoom) < 0.01)
    for (const type of ['text', 'barcode', 'qrcode', 'image', 'icon', 'line', 'rect']) {
      await page.evaluate(t => { const d = window.__labelDesigner.designer; d.selection = [d.template.elements.find(e => e.type === t).id] }, type)
      await page.waitForTimeout(50)
      if (mode === 'after') await interact('.props .seg', type)
      await measure(type + '-' + suffix)
    }
    await nav('apitest')
    if (mode === 'after') await interact('.seg', 'API 输出格式')
    await measure('api-' + suffix)
    await nav('templates')
    if (mode === 'after') await interact('.seg', '模板筛选与显示方式')
    await page.locator('.sidebar-heading button').click()
    await page.getByRole('dialog').waitFor()
    if (mode === 'after') await interact('[role=dialog] .seg', '新模板方向')
    await measure('new-dialog-' + suffix)
    await page.getByRole('dialog').getByRole('button', { name: '取消', exact: true }).click()
    await nav('t/BOX_LABEL/designer')
    await page.getByRole('button', { name: '服务端预览', exact: true }).click()
    await page.getByRole('dialog').waitFor()
    if (mode === 'after') await interact('[role=dialog] .seg', '预览 DPI')
    await measure('preview-dialog-' + suffix)
    await page.getByRole('dialog').getByRole('button', { name: '关闭', exact: true }).click()
    await nav('t/' + code + '/designer')
  }
  check('无浏览器异常', errors.length === 0)
} catch (error) { errors.push(String(error)); process.exitCode = 1 }
finally {
  await fs.writeFile(path.join(output, 'results.json'), JSON.stringify({ mode, results, checks, errors, problematic: results.flatMap(r => r.groups.filter(g => g.gap > 1.5).map(g => ({ name: r.name, label: g.label, gap: g.gap }))) }, null, 2))
  if (context) await context.close()
  console.log(JSON.stringify({ mode, screenshots: results.length, checks: checks.length, errors }))
}
