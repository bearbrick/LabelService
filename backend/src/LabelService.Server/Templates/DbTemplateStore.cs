using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Server.Templates;

/// <summary>
/// 已发布模板的内存缓存：启动时由 <see cref="TemplateRepository.LoadPublishedAsync"/> 从数据库整体加载，发布时追加。
/// 开放接口只读这里，不访问数据库（R3）。
/// </summary>
/// <remarks>
/// 读多写少：读不加锁，直接读当前快照；写（发布）加锁复制出新快照再整体替换。单实例部署（ADR 0003），不需要跨实例同步。
/// </remarks>
internal sealed class DbTemplateStore : ITemplateStore
{
    private readonly Lock gate = new();
    private volatile FrozenDictionary<string, TemplateVersions> templates = FrozenDictionary<string, TemplateVersions>.Empty;

    /// <summary>
    /// 缓存里的模板数。
    /// </summary>
    public int Count => templates.Count;

    /// <inheritdoc />
    public bool TryGetPublished(string templateCode, int? version, [NotNullWhen(true)] out PublishedTemplate? template)
    {
        template = templates.TryGetValue(templateCode, out var versions)
            ? version is null ? versions.Latest : versions.All.GetValueOrDefault(version.Value)
            : null;
        if (template is null)
        {
            Lookups.Miss();
            return false;
        }

        Lookups.Hit();
        return true;
    }

    /// <inheritdoc />
    public CacheCounter Lookups { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<PublishedTemplate> LatestPublished() => templates.Values.Select(versions => versions.Latest).ToArray();

    /// <summary>
    /// 用数据库里的全部已发布版本替换缓存。
    /// </summary>
    /// <param name="published">模板编码和已发布版本。</param>
    public void Load(IEnumerable<(string Code, PublishedTemplate Template)> published)
    {
        var next = published
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .ToFrozenDictionary(
                group => group.Key,
                group => TemplateVersions.From(group.Select(item => item.Template)),
                StringComparer.Ordinal);
        lock (gate)
        {
            templates = next;
        }
    }

    /// <summary>
    /// 发布了一个新版本：立即生效。版本号比当前最新版本小时（并发发布的通知晚到）只补进历史版本，不改最新版本。
    /// </summary>
    /// <param name="code">模板编码。</param>
    /// <param name="published">新发布的版本。</param>
    public void Add(string code, PublishedTemplate published)
    {
        lock (gate)
        {
            var next = new Dictionary<string, TemplateVersions>(templates, StringComparer.Ordinal);
            next[code] = next.TryGetValue(code, out var versions)
                ? TemplateVersions.From(versions.All.Values.Where(item => item.Version != published.Version).Append(published))
                : TemplateVersions.From([published]);
            templates = next.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 模板换了业务分组：更新全部已发布版本的分组，调用方权限立即按新分组判断。
    /// </summary>
    public void SetBusiness(string code, int? businessId)
    {
        lock (gate)
        {
            if (!templates.TryGetValue(code, out var versions))
            {
                return;
            }

            var next = new Dictionary<string, TemplateVersions>(templates, StringComparer.Ordinal)
            {
                [code] = TemplateVersions.From(versions.All.Values.Select(item => item with { BusinessId = businessId })),
            };
            templates = next.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 模板删除了：开放接口立即不能再用。
    /// </summary>
    public void Remove(string code)
    {
        lock (gate)
        {
            if (templates.ContainsKey(code))
            {
                templates = templates.Where(item => item.Key != code).ToFrozenDictionary(StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// 一个模板的全部已发布版本。
    /// </summary>
    private sealed record TemplateVersions(PublishedTemplate Latest, FrozenDictionary<int, PublishedTemplate> All)
    {
        public static TemplateVersions From(IEnumerable<PublishedTemplate> versions)
        {
            var all = versions.ToFrozenDictionary(item => item.Version);
            return new TemplateVersions(all.Values.MaxBy(item => item.Version)!, all);
        }
    }
}
