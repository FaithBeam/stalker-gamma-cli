using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Stalker.Gamma.IntegrationTests.Cli;

/// <summary>
/// Serves <see cref="Body"/> to every request on 127.0.0.1, standing in for the online mod
/// pack maker list
/// </summary>
public sealed class ListServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();

    public string Body { get; set; } = "";

    public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/list";

    public ListServer()
    {
        _listener.Start();
        _ = Task.Run(AcceptAsync);
    }

    private async Task AcceptAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            _ = Task.Run(() => RespondAsync(client));
        }
    }

    private async Task RespondAsync(TcpClient client)
    {
        using var _ = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        // the request line and headers end with a blank line
        while (await reader.ReadLineAsync(_cts.Token) is { Length: > 0 }) { }

        var body = Encoding.UTF8.GetBytes(Body);
        var head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/plain; charset=utf-8\r\n"
                + $"Content-Length: {body.Length}\r\n"
                + "Connection: close\r\n\r\n"
        );
        await stream.WriteAsync(head, _cts.Token);
        await stream.WriteAsync(body, _cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
