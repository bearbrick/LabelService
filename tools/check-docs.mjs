/**
 * 检查 Git 管理和新增 Markdown 中的本地文件链接；不访问外部网站，不检查页面锚点。
 */
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * 仓库根目录，不依赖调用时所在目录。
 */
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')

/**
 * 提取 Markdown 行内链接、图片和引用定义，跳过代码块及行内代码。
 * @param content Markdown 内容。
 * @returns 原始目标及行号。
 */
function targets(content) {
  const found = []
  let fence = null
  for (const [index, original] of content.split(/\r?\n/).entries()) {
    const marker = /^ {0,3}(`{3,}|~{3,})/.exec(original)?.[1]
    if (marker) {
      if (!fence) fence = marker
      else if (marker[0] === fence[0] && marker.length >= fence.length) fence = null
      continue
    }
    if (fence) continue
    const line = original.replace(/(`+).*?\1/g, '')
    for (const match of line.matchAll(/\]\((<[^>\r\n]+>|[^)\r\n]+)\)/g)) {
      found.push({ target: match[1], line: index + 1 })
    }
    const reference = /^ {0,3}\[[^\]]+\]:\s*(.+)$/.exec(line)
    if (reference) found.push({ target: reference[1], line: index + 1 })
  }
  return found
}

/**
 * 去掉链接标题、锚点及 URL 查询，保留可检查的本地路径。
 * @param target 原始链接目标。
 * @returns 解码后的本地路径；外部链接、页内锚点为空。
 */
function localPath(target) {
  const trimmed = target.trim()
  const destination = trimmed.startsWith('<')
    ? trimmed.slice(1, trimmed.indexOf('>'))
    : trimmed.split(/\s+["']/)[0].trim()
  if (!destination || destination.startsWith('#') || /^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(destination)) return null
  return decodeURIComponent(destination.split(/[?#]/)[0]) || null
}

/**
 * 检查目标是否在仓库内且存在，路径以来源文件为基准解析。
 * @param source 来源文件，相对于仓库。
 * @param target 原始链接。
 * @param exists 文件存在判断；检查自身时可替换，避免创建或删除测试文件。
 * @returns 失败原因；成功或外部链接返回空。
 */
function problem(source, target, exists = existsSync) {
  let path
  try { path = localPath(target) }
  catch { return '链接的 URL 编码不合法' }
  if (!path) return null
  const absolute = resolve(root, dirname(source), path)
  const inside = relative(root, absolute)
  if (isAbsolute(path) || isAbsolute(inside) || inside === '..' || inside.startsWith('..\\') || inside.startsWith('../')) {
    return '本地链接必须指向仓库内文件'
  }
  return exists(absolute) ? null : `目标不存在：${path}`
}

/**
 * 自检覆盖真实断链、代码示例、引用定义和带空格的路径，不改磁盘。
 */
function selfTest() {
  const parsed = targets('[入口](README.md)\n```md\n[示例](missing.md)\n```\n`[代码](missing.md)`\n![图](docs/a%20b.png)\n[ref]: <docs/a b.md> "标题"')
  assert.deepEqual(parsed.map(item => item.target), ['README.md', 'docs/a%20b.png', '<docs/a b.md> "标题"'])
  assert.equal(localPath('<docs/a b.md> "标题"'), 'docs/a b.md')
  assert.equal(localPath('docs/a%20b.md#段落'), 'docs/a b.md')
  assert.equal(problem('README.md', 'missing.md', () => false), '目标不存在：missing.md')
  assert.equal(problem('README.md', 'https://example.com/any', () => false), null)
  assert.equal(problem('docs/README.md', '../README.md', () => true), null)
  assert.equal(problem('README.md', '../outside.md', () => true), '本地链接必须指向仓库内文件')
  assert.equal(problem('README.md', '%ZZ.md', () => true), '链接的 URL 编码不合法')
}

/**
 * 执行本地文档检查，列出全部断链后返回失败状态。
 */
function main() {
  selfTest()
  if (process.argv.includes('--self-test')) {
    console.log('文档链接检查自检通过')
    return
  }
  const files = [...new Set(execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z', '--', '*.md'], {
    cwd: root, encoding: 'utf8',
  }).split('\0').filter(Boolean))]
  const failures = []
  let count = 0
  for (const file of files) {
    if (!existsSync(resolve(root, file))) continue
    for (const item of targets(readFileSync(resolve(root, file), 'utf8'))) {
      const error = problem(file, item.target)
      if (error) {
        count++
        failures.push(`${file}:${item.line} ${error}`)
      } else if (localPath(item.target)) count++
    }
  }
  if (failures.length) {
    console.error(`文档链接检查失败：${failures.length} 处\n${failures.join('\n')}`)
    process.exitCode = 1
  } else console.log(`文档链接检查通过：${files.length} 个 Markdown，${count} 个本地文件链接（不含锚点和外部网站）`)
}

main()
