using System.Net;

namespace SuikodenCodex.StarLeap.Tests;

public sealed class FakeServer : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files;
    private int _inFlight;

    public FakeServer(Dictionary<string, byte[]> files) => _files = files;

    public List<string> Requested { get; } = new();
    public bool Offline { get; set; }
    public Dictionary<string, byte[]> Overrides { get; } = new();
    public int DelayMs { get; set; }
    public int MaxInFlight { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Offline)
            throw new HttpRequestException("offline");
        var now = Interlocked.Increment(ref _inFlight);
        lock (Requested)
        {
            if (now > MaxInFlight)
                MaxInFlight = now;
        }
        try
        {
            if (DelayMs > 0)
                await Task.Delay(DelayMs, ct);
            var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Replace("/pack/", ""));
            lock (Requested)
            {
                Requested.Add(path);
            }
            var body = Overrides.TryGetValue(path, out var o) ? o : _files.GetValueOrDefault(path);
            return body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}
