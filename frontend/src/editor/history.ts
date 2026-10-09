/**
 * 撤销重做：保存模板的 JSON 快照。快照和当前一致时不入栈，超过上限丢掉最早的。
 */
export class History {
  private stack: string[] = []
  private index = -1
  private readonly limit: number

  constructor(limit = 100) {
    this.limit = limit
  }

  /** 清空历史，以当前状态作为起点。 */
  reset(snapshot: string): void {
    this.stack = [snapshot]
    this.index = 0
  }

  /** 记录一次修改。和当前快照相同时返回 false。 */
  push(snapshot: string): boolean {
    if (this.stack[this.index] === snapshot) return false
    this.stack.splice(this.index + 1)
    this.stack.push(snapshot)
    if (this.stack.length > this.limit) this.stack.shift()
    this.index = this.stack.length - 1
    return true
  }

  /** 丢掉当前位置之后的记录（撤回一次不允许的修改后用，不能再重做出来）。 */
  dropRedo(): void {
    this.stack.splice(this.index + 1)
  }

  /** 回到上一步，返回要恢复的快照；没有可撤销的返回 null。 */
  undo(): string | null {
    if (!this.canUndo) return null
    this.index--
    return this.stack[this.index]
  }

  /** 前进一步，返回要恢复的快照；没有可重做的返回 null。 */
  redo(): string | null {
    if (!this.canRedo) return null
    this.index++
    return this.stack[this.index]
  }

  get canUndo(): boolean {
    return this.index > 0
  }

  get canRedo(): boolean {
    return this.index < this.stack.length - 1
  }

  /** 当前可撤销的步数。 */
  get undoDepth(): number {
    return this.index
  }
}
