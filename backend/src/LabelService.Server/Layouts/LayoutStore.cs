using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LabelService.Contracts.Layouts;

namespace LabelService.Server.Layouts;

/// <summary>
/// 启用中的拼版版式的内存快照。开放接口只读这里，不访问数据库（R3）。
/// </summary>
/// <remarks>
/// 启动时从数据库（或版式目录）整体加载，管理后台改了版式立即替换。读不加锁，写加锁复制出新快照再整体替换（和模板缓存一样）。
/// 放进来的 <see cref="SheetLayout"/> 实例之后不再修改，多个请求共享。
/// </remarks>
internal sealed class LayoutStore
{
    private readonly Lock gate = new();
    private volatile FrozenDictionary<string, SheetLayout> layouts = FrozenDictionary<string, SheetLayout>.Empty;

    /// <summary>
    /// 启用中的版式数。
    /// </summary>
    public int Count => layouts.Count;

    /// <summary>
    /// 取启用中的版式。
    /// </summary>
    public bool TryGet(string code, [NotNullWhen(true)] out SheetLayout? layout) => layouts.TryGetValue(code, out layout);

    /// <summary>
    /// 用全部启用中的版式替换快照。
    /// </summary>
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
    public void Remove(string code) => Set(new SheetLayout { Code = code }, enabled: false);
}
