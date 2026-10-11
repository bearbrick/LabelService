using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using LabelService.Rendering.Diagnostics;

namespace LabelService.Application.Templates;

/// <summary>
/// 已发布模板的内存缓存。开放接口只从这里取模板，请求路径上不访问数据库和磁盘（AGENTS.md R3）。
/// </summary>
/// <remarks>
/// <para>
/// 启动时整体加载（数据库里的全部已发布版本，或模板目录里的 JSON，见基础设施层的启动初始化），发布、换分组、删除时立即更新。
/// </para>
/// <para>
/// 读多写少：读不加锁，直接读当前快照；写加锁复制出新快照再整体替换。单实例部署（ADR 0003），不需要跨实例同步。
/// </para>
/// </remarks>
public sealed class PublishedTemplateCache
{
    /// <summary>
    /// 写操作之间的锁。读不用锁。
    /// </summary>
    private readonly Lock gate = new();

    /// <summary>
    /// 当前快照：模板编码 → 这个模板的全部已发布版本。
    /// </summary>
    private volatile FrozenDictionary<string, TemplateVersions> templates = FrozenDictionary<string, TemplateVersions>.Empty;

    /// <summary>
    /// 缓存里的模板数。
    /// </summary>
    public int Count => templates.Count;

    /// <summary>
    /// 查找次数：找到算命中，模板或版本不存在算未命中（指标 label_cache_hit_ratio 的“模板”一项）。
    /// </summary>
    public CacheCounter Lookups { get; } = new();

    /// <summary>
    /// 取已发布的模板版本。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="version">版本号；null 表示最新发布版本。</param>
    /// <param name="template">找到的模板。</param>
    /// <returns>模板不存在、版本不存在或从未发布时返回 false。</returns>
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

    /// <summary>
    /// 每个模板的最新发布版本（启动预热用）。
    /// </summary>
    /// <returns>每个模板一项。</returns>
    public IReadOnlyList<PublishedTemplate> LatestPublished() => templates.Values.Select(versions => versions.Latest).ToArray();

    /// <summary>
    /// 用全部已发布版本替换缓存。
    /// </summary>
    /// <param name="published">模板编码和已发布版本；同一个编码可以有多个版本。</param>
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
    /// <param name="code">模板编码。</param>
    /// <param name="businessId">新的业务分组。</param>
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
    /// <param name="code">模板编码。</param>
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
    /// 一个模板的全部已发布版本和其中最新的一个。
    /// </summary>
    /// <param name="Latest">版本号最大的版本。</param>
    /// <param name="All">版本号 → 版本。</param>
    private sealed record TemplateVersions(PublishedTemplate Latest, FrozenDictionary<int, PublishedTemplate> All)
    {
        /// <summary>
        /// 由一组版本创建。
        /// </summary>
        /// <param name="versions">同一个模板的已发布版本，至少一个。</param>
        /// <returns>版本集合。</returns>
        public static TemplateVersions From(IEnumerable<PublishedTemplate> versions)
        {
            var all = versions.ToFrozenDictionary(item => item.Version);
            return new TemplateVersions(all.Values.MaxBy(item => item.Version)!, all);
        }
    }
}
