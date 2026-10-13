using LabelService.Client.Internal;

namespace LabelService.Client;

/// <summary>
/// 在已有客户端接口上提供批量集合能力，不要求现有接口实现者增加成员。
/// </summary>
public static class LabelServiceClientExtensions
{
    /// <summary>
    /// 一次生成全部标签并返回可打印集合，普通标签按条目分组，拼版按纸页分组。
    /// </summary>
    /// <param name="client">应用内共用的客户端。</param>
    /// <param name="request">批量请求，方法不修改原请求的输出方式或数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含原始响应及可打印项的集合。</returns>
    public static async Task<LabelBatchResult> RenderBatchFilesAsync(this ILabelServiceClient client,
        BatchRenderRequest request, CancellationToken cancellationToken = default)
    {
        if (client == null)
        {
            throw new ArgumentNullException(nameof(client));
        }
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = BatchFiles.CopyRequest(request);
        var file = await client.RenderBatchAsync(snapshot, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return BatchFiles.Read(file, snapshot, cancellationToken);
    }

    /// <summary>
    /// 同步生成批量集合，界面代码建议使用异步方法。
    /// </summary>
    /// <param name="client">应用内共用的客户端。</param>
    /// <param name="request">批量请求。</param>
    /// <returns>生成后的可打印集合。</returns>
    public static LabelBatchResult RenderBatchFiles(this ILabelServiceClient client, BatchRenderRequest request) =>
        Task.Run(() => client.RenderBatchFilesAsync(request)).GetAwaiter().GetResult();
}
