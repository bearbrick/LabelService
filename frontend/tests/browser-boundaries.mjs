/**
 * 真实浏览器边界回归，只访问自建且已有合成测试数据的服务实例。
 */
import fs from 'node:fs/promises'
import path from 'node:path'
import { createRequire } from 'node:module'
import assert from 'node:assert/strict'

const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const url = process.env.LABEL_UI_URL
const password = process.env.LABEL_UI_PASSWORD
const fixture = JSON.parse(await fs.readFile(process.env.LABEL_UI_FIXTURE, 'utf8'))
assert(url && password && fixture.appId && fixture.name && fixture.code, 'Explicit isolated URL, credentials and long fixture required')
const output = process.env.LABEL_UI_EVIDENCE || 'browser-boundary-evidence'
await fs.mkdir(output, { recursive: true })
const browser = await chromium.launch({ channel: 'chrome', headless: true })
const results = []
const errors = []
try {
  const page = await browser.newPage()
  page.on('pageerror', e => errors.push(String(e)))
  await page.goto(url, { waitUntil: 'networkidle' })
  await page.locator('#lg-account').fill(process.env.LABEL_UI_ACCOUNT || 'admin')
  await page.locator('#lg-pwd').fill(password)
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await page.locator('.topbar').waitFor()
  for (const [width, height] of [[1366, 768], [1920, 1080], [1280, 720], [1366, 600], [1024, 600]]) {
    await page.setViewportSize({ width, height })
    for (const route of ['business', 'clients', 'users', 'roles', 'oplogs', 'presets', `t/${fixture.code}/designer`]) {
      await page.evaluate(r => { location.hash = '#/' + r }, route)
      await page.waitForTimeout(600)
      if (route === 'clients') await page.getByText(fixture.appId, { exact: true }).scrollIntoViewIfNeeded()
      if (route === 'users') await page.getByText(fixture.account, { exact: true }).scrollIntoViewIfNeeded()
      if (route === 'business') assert(await page.locator('.business-table td').filter({ hasText: fixture.name }).count(), 'Long owner must actually be present')
      const overflow = await page.locator('.dt td,.dt th').evaluateAll(cells => cells
        .filter(x => x.clientWidth && x.scrollWidth > x.clientWidth + 2)
        .map(x => ({ text: x.textContent, column: x.cellIndex, width: x.clientWidth, scrollWidth: x.scrollWidth })))
      const documentOverflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)
      if (documentOverflow) overflow.push({ document: true })
      if (route.endsWith('/designer')) {
        const workspace = page.locator('.tpl-ws')
        await workspace.hover({ position: { x: 20, y: 20 } })
        await page.mouse.wheel(0, 2000)
        await page.waitForTimeout(100)
        const reachable = await workspace.evaluate(x => {
          const rect = element => element.getBoundingClientRect()
          const inside = element => { const r = rect(element); return r.height > 0 && r.top >= 0 && r.bottom <= innerHeight + 1 }
          const content = innerWidth <= 1100 ? x.querySelector('.narrow-note .in') : x.querySelector('.designer')
          return inside(x.querySelector('.tabs')) && inside(content)
        })
        if (!reachable) overflow.push({ workspaceContentUnreachable: true })
      }
      const name = `${route.replaceAll('/', '_')}-${width}x${height}`
      await page.screenshot({ path: path.join(output, name + '.png') })
      results.push({ name, overflow })
    }
  }
  assert.deepEqual(errors, [], 'Browser exceptions')
  assert.deepEqual(results.filter(r => r.overflow.length), [], 'Cells must not obscure neighbouring columns')
} finally {
  await fs.writeFile(path.join(output, 'results.json'), JSON.stringify({ results, errors }, null, 2))
  await browser.close()
}
