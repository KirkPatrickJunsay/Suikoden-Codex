using System.Net;

namespace SuikodenCodex.StarLeap.Tests;

public sealed class FakeServer : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files;

    public FakeServer(Dictionary<string, byte[]> files) => _files = files;

    public List<string> Requested { get; } = new();
    public bool Offline { get; set; }
    public Dictionary<string, byte[]> Overrides { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Offline)
            throw new HttpRequestException("offline");
        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Replace("/pack/", ""));
        Requested.Add(path);
        var body = Overrides.TryGetValue(path, out var o) ? o : _files.GetValueOrDefault(path);
        return Task.FromResult(body is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
