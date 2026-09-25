namespace SuikodenCodex.StarLeap;

public interface IPackFiles
{
    Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default);
}

public sealed class DirectoryPackFiles : IPackFiles
{
    private readonly string _root;

    public DirectoryPackFiles(string root) => _root = root;

    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }
}
