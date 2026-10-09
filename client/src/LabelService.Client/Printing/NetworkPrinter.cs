using System.Net.Sockets;

namespace LabelService.Client.Printing;

/// <summary>
/// 通过 TCP（通常是 9100 端口）把 ZPL 等打印机指令直接发给网络标签打印机。
/// </summary>
public static class NetworkPrinter
{
    /// <summary>默认端口。</summary>
    public const int DefaultPort = 9100;

    /// <summary>
    /// 发送指令。打印机收到数据即返回，不代表已经打印完成。
    /// </summary>
    /// <param name="host">打印机 IP 或主机名。</param>
    /// <param name="port">端口，一般是 9100。</param>
    /// <param name="content">要发送的字节，如 <see cref="LabelFile.Content"/>。</param>
    /// <param name="connectTimeout">连接超时，默认 10 秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示发送操作的任务。</returns>
    /// <exception cref="TimeoutException">连接超时。</exception>
    /// <exception cref="SocketException">连接被拒绝等网络错误。</exception>
    public static async Task SendAsync(
        string host,
        int port,
        byte[] content,
        TimeSpan? connectTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (host == null || host.Trim().Length == 0)
        {
            throw new ArgumentException("必须指定打印机地址。", nameof(host));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        using (var client = new TcpClient())
        {
            var connect = client.ConnectAsync(host, port);
            var timeout = Task.Delay(connectTimeout ?? TimeSpan.FromSeconds(10), cancellationToken);
            if (await Task.WhenAny(connect, timeout).ConfigureAwait(false) != connect)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"连接打印机 {host}:{port} 超时。");
            }

            await connect.ConfigureAwait(false);
            using (var stream = client.GetStream())
            {
                await stream.WriteAsync(content, 0, content.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 同步发送指令。
    /// </summary>
    /// <param name="host">打印机 IP 或主机名。</param>
    /// <param name="port">端口，一般是 9100。</param>
    /// <param name="content">要发送的字节。</param>
    public static void Send(string host, int port, byte[] content) =>
        Task.Run(() => SendAsync(host, port, content)).GetAwaiter().GetResult();
}
