using LabelService.Application.Dashboard.Dtos;

namespace LabelService.Application.Dashboard;

/// <summary>
/// 概览页（权限点 tpl.view）：模板数量；有 log.view 时再给今天的调用（含单张 P95）、近 14 天每天的调用量和失败数、
/// 近 7 天调用最多和最慢的模板、最近的操作。“今天”和每一天按服务器所在时区划分。
/// </summary>
public interface IDashboardAppService
{
    /// <summary>
    /// 概览数据。
    /// </summary>
    /// <returns>概览。</returns>
    Task<DashboardDto> GetAsync();
}
