namespace LabelService.Client;

/// <summary>
/// 标签服务客户端。调用方写单元测试时可以替换成自己的实现。
/// </summary>
public interface ILabelServiceClient
{
    /// <summary>
    /// 每次调用结束时触发（成功或失败），在调用线程上同步执行，处理函数里不要做耗时操作。
    /// </summary>
    event EventHandler<CallCompletedEventArgs>? CallCompleted;

    /// <summary>
    /// 生成单张标签。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>标签文件。</returns>
    /// <exception cref="ArgumentException">请求参数在本地校验不通过，没有发出请求。</exception>
    /// <exception cref="LabelServiceException">服务端返回错误或网络错误。</exception>
    Task<LabelFile> RenderAsync(RenderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成单张标签（同步）。界面线程里建议用 <see cref="RenderAsync"/>。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <returns>标签文件。</returns>
    LabelFile Render(RenderRequest request);

    /// <summary>
    /// 批量生成，返回合并后的文件或 ZIP 包。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>标签文件。</returns>
    /// <exception cref="ArgumentException">请求参数在本地校验不通过，没有发出请求。</exception>
    /// <exception cref="LabelServiceException">服务端返回错误或网络错误。</exception>
    Task<LabelFile> RenderBatchAsync(BatchRenderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成（同步）。
    /// </summary>
    /// <param name="request">请求。</param>
    /// <returns>标签文件。</returns>
    LabelFile RenderBatch(BatchRenderRequest request);

    /// <summary>
    /// 读取模板最新发布版本的字段定义和样例。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>字段定义。</returns>
    /// <exception cref="LabelServiceException">服务端返回错误或网络错误。</exception>
    Task<TemplateSchema> GetSchemaAsync(string templateCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取字段定义（同步）。
    /// </summary>
    /// <param name="templateCode">模板编码。</param>
    /// <returns>字段定义。</returns>
    TemplateSchema GetSchema(string templateCode);
}
