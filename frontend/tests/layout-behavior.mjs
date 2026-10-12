/**
 * 真实浏览器中的导航、视口和窄屏边界回归；只访问显式指定的隔离实例。
 */
import fs from 'node:fs/promises'
import path from 'node:path'
import assert from 'node:assert/strict'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const url = process.env.LABEL_UI_URL
const password = process.env.LABEL_UI_PASSWORD
const code = process.env.LABEL_UI_TEMPLATE || 'BOX_LABEL'
const output = process.env.LABEL_UI_EVIDENCE
assert(url?.startsWith('http://127.0.0.1:') && password && output, 'Explicit isolated loopback URL, credentials and evidence directory required')
await fs.mkdir(output, { recursive: true })
const checks = [], errors = []
const browser = await chromium.launch({ channel: 'chrome', headless: true })
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } })
page.on('pageerror', error => errors.push(String(error)))

/**
 * 记录一个回归断言，失败时立即停止。
 * @param name 检查说明。
 * @param value 判断结果。
 * @param evidence 对应状态。
 */
function check(name, value, evidence) { checks.push({ name, passed: value, evidence }); assert(value, name) }

/**
 * 等待两个绘制帧及异步组件监听。
 */
async function settle() { await page.waitForTimeout(180); await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))) }

/**
 * 获取真实视口状态。
 * @returns 缩放、位置和尺寸。
 */
async function state() { await settle(); return page.evaluate(() => window.__layoutViews.snapshot()) }

try {
  await page.goto(url, { waitUntil: 'networkidle' })
  await page.locator('#lg-account').fill(process.env.LABEL_UI_ACCOUNT || 'admin')
  await page.locator('#lg-pwd').fill(password)
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await page.locator('.topbar').waitFor()
  await page.evaluate(() => { location.hash = '#/profile' }); await settle()
  await page.evaluate(async () => {
    const harness = await import('/tests/layout-browser-harness.ts')
    const host = document.createElement('div'); host.id = 'layout-regression-host'; host.style.cssText = 'position:fixed;left:300px;top:80px;z-index:1000'; document.body.append(host)
    const add = window.addEventListener, remove = window.removeEventListener
    const listeners = new Set()
    window.addEventListener = function(type, listener, ...rest) { if (type === 'resize') listeners.add(listener); return add.call(this, type, listener, ...rest) }
    window.removeEventListener = function(type, listener, ...rest) { if (type === 'resize') listeners.delete(listener); return remove.call(this, type, listener, ...rest) }
    window.__layoutNav = harness.navigation(host)
    window.__navListeners = () => listeners.size
    window.__restoreNavigation = () => { window.addEventListener = add; window.removeEventListener = remove; host.remove() }
  })
  check('1280 初次打开自动收起模板树', await page.evaluate(() => window.__layoutNav.collapsed()))
  await page.locator('#layout-regression-host button').click()
  await page.setViewportSize({ width: 1279, height: 720 }); await settle()
  check('窄屏手动展开后继续缩小不会再次强制收起', !await page.evaluate(() => window.__layoutNav.collapsed()))
  await page.setViewportSize({ width: 1440, height: 900 }); await settle()
  check('1440 门槛外保留手动展开', !await page.evaluate(() => window.__layoutNav.collapsed()))
  await page.setViewportSize({ width: 1439, height: 900 }); await settle()
  check('1440 缩到 1439 自动收起', await page.evaluate(() => window.__layoutNav.collapsed()))
  await page.locator('#layout-regression-host button').click()
  await page.evaluate(async () => { await window.__layoutNav.setAuto(false); await window.__layoutNav.setAuto(true) })
  check('从其他页签切回设计时按窄屏收起', await page.evaluate(() => window.__layoutNav.collapsed()))
  check('导航只登记一个 resize 监听', await page.evaluate(() => window.__navListeners() === 1))
  const remaining = await page.evaluate(() => { window.__layoutNav.dispose(); return window.__navListeners() })
  check('导航卸载移除 resize 监听', remaining === 0, remaining)
  await page.evaluate(() => window.__restoreNavigation())
  await page.evaluate(async () => {
    const harness = await import('/tests/layout-browser-harness.ts')
    const host = document.createElement('div'); host.id = 'layout-viewport-host'; host.style.cssText = 'position:fixed;left:0;top:0;z-index:1000'; document.body.append(host)
    window.__layoutViews = harness.viewports(host)
  })
  let before = await state()
  await page.evaluate(() => window.__layoutViews.resize(600, 450)); let after = await state()
  for (let i = 0; i < 2; i++) check(`${i ? '拼版' : '模板'}适应状态随尺寸变化重新适应`, after[i].zoom < before[i].zoom && after[i].size.width === 600, { before: before[i], after: after[i] })
  await page.evaluate(() => window.__layoutViews.zoom()); before = await state()
  await page.evaluate(() => window.__layoutViews.resize(500, 350)); after = await state()
  for (let i = 0; i < 2; i++) check(`${i ? '拼版' : '模板'}手动缩放在 resize 后保持`, Math.abs(after[i].zoom - before[i].zoom) < 1e-8, { before: before[i], after: after[i] })
  await page.evaluate(() => window.__layoutViews.fit()); before = await state()
  await page.evaluate(() => window.__layoutViews.pan()); let panned = await state()
  await page.evaluate(() => window.__layoutViews.resize(450, 300)); after = await state()
  for (let i = 0; i < 2; i++) check(`${i ? '拼版' : '模板'}手动平移后 resize 保留视图`, panned[i].position.x !== before[i].position.x && JSON.stringify(panned[i].position) === JSON.stringify(after[i].position) && after[i].zoom === panned[i].zoom, { before: before[i], panned: panned[i], after: after[i] })
  await page.evaluate(() => window.__layoutViews.fit()); before = await state()
  await page.evaluate(() => window.__layoutViews.resize(700, 500)); after = await state()
  for (let i = 0; i < 2; i++) check(`${i ? '拼版' : '模板'}再次适应恢复 resize 跟随`, after[i].zoom > before[i].zoom, { before: before[i], after: after[i] })
  await page.evaluate(() => { window.__layoutViews.zoom(); window.__layoutViews.requestFit() }); before = await state()
  await page.evaluate(() => window.__layoutViews.resize(800, 600)); after = await state()
  check('模板变更 requestFit 覆盖旧手动视图并恢复跟随', after[0].zoom > before[0].zoom, { before: before[0], after: after[0] })
  await page.evaluate(() => { window.__layoutViews.dispose(); document.querySelector('#layout-viewport-host').remove() })

  for (const route of [`t/${code}/designer`, 'layouts/A4_100x60_2x4']) {
    for (const width of [1100, 1101]) {
      await page.setViewportSize({ width, height: 700 })
      await page.evaluate(r => { location.hash = '#/' + r }, route); await settle()
      const metrics = await page.locator('.designer').evaluate(el => {
        const visible = x => getComputedStyle(x).visibility !== 'hidden' && getComputedStyle(x).display !== 'none'
        const note = el.querySelector('.narrow-note')
        const stage = el.querySelector('.stage-host,.sheet-stage')
        return { width: innerWidth, documentWidth: document.documentElement.scrollWidth, noteVisible: !!note && visible(note), stageVisible: !!stage && visible(stage), stageWidth: stage?.clientWidth, toolbarVisible: visible(el.querySelector('.toolbar')) }
      })
      check(`${route} ${width}px 提示与可操作区互斥`, width === 1100 ? metrics.noteVisible && !metrics.stageVisible && !metrics.toolbarVisible : !metrics.noteVisible && metrics.stageVisible && metrics.stageWidth > 400, metrics)
      check(`${route} ${width}px 不产生页面横向溢出`, metrics.documentWidth <= width + 1, metrics)
      if (width === 1100) {
        for (let n = 0; n < 12; n++) await page.keyboard.press('Tab')
        check(`${route} 窄屏不聚焦被隐藏工具栏`, await page.evaluate(() => !document.activeElement?.closest('.designer > :not(.narrow-note)')))
      }
      await page.screenshot({ path: path.join(output, route.replaceAll('/', '_') + '-' + width + '.png') })
    }
  }
  for (let n = 0; n < 4; n++) {
    await page.evaluate(() => { location.hash = '#/layouts/A4_100x60_2x4' }); await settle()
    await page.evaluate(() => { location.hash = '#/profile' }); await settle()
  }
  check('重复挂载和卸载后没有浏览器异常', errors.length === 0, errors)
} finally {
  await fs.writeFile(path.join(output, 'layout-behavior-results.json'), JSON.stringify({ checks, errors }, null, 2))
  await browser.close()
}
console.log(JSON.stringify({ checks: checks.length, passed: checks.every(x => x.passed), errors }))
