using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LabelService.Contracts.Layouts;

namespace LabelService.Application.Layouts;

/// <summary>
/// 启用中的拼版版式的内存快照。开放接口只从这里取版式，请求路径上不访问数据库（AGENTS.md R3）。
/// </summary>
/// <remarks>
/// 启动时整体加载，管理后台改了版式立即替换。读不加锁，写加锁复制出新快照再整体替换（和模板缓存一样）。
/// 放进来的 <see cref="SheetLayout"/> 实例之后不再修改，多个请求共享。
/// </remarks>
public sealed class SheetLayoutCache
{
    /// <summary>
    /// 写操作之间的锁。读不用锁。
    /// </summary>
    private readonly Lock gate = new();

    /// <summary>
    /// 当前快照：版式编码 → 版式。
    /// </summary>
    private volatile FrozenDictionary<string, SheetLayout> layouts = FrozenDictionary<string, SheetLayout>.Empty;

    /// <summary>
    /// 启用中的版式数。
    /// </summary>
    public int Count => layouts.Count;

    /// <summary>
    /// 取启用中的版式。
    /// </summary>
    /// <param name="code">版式编码。</param>
    /// <param name="layout">找到的版式。</param>
    /// <returns>不存在或已停用时返回 false。</returns>
    public bool TryGet(string code, [NotNullWhen(true)] out SheetLayout? layout) => layouts.TryGetValue(code, out layout);

    /// <summary>
    /// 用全部启用中的版式替换快照。
    /// </summary>
    /// <param name="enabled">启用中的版式。</param>
    public void Load(IEnumerable<SheetLayout> enabled)
    {
        var next = enabled.ToFrozenDictionary(layout => layout.Code, StringComparer.Ordinal);
        lock (gate)
        {
            layouts = next;
        }
    }

    /// <summary>
    /// 新建或修改了一个版式：启用的放进快照，停用的拿掉。
    /// </summary>
    /// <param name="layout">版式。</param>
    /// <param name="enabled">是否启用。</param>
    public void Set(SheetLayout layout, bool enabled)
    {
        lock (gate)
        {
            var next = new Dictionary<string, SheetLayout>(layouts, StringComparer.Ordinal);
            if (enabled)
            {
                next[layout.Code] = layout;
            }
            else
            {
                next.Remove(layout.Code);
            }

            layouts = next.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 版式删除了：开放接口立即不能再用。
    /// </summary>
    /// <param name="code">版式编码。</param>
    public void Remove(string code) => Set(new SheetLayout { Code = code }, enabled: false);
}
