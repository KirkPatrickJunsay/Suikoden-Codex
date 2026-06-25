namespace SuikodenCodex.Services;

/// <summary>Captures a visual element to a PNG and opens the share sheet.</summary>
public static class ShareService
{
    public static async Task ShareElementAsync(VisualElement element, string title, string fileName)
    {
        if (!element.IsVisible) return;
        var result = await element.CaptureAsync();
        if (result is null) return;

        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        using (var src = await result.OpenReadAsync())
        using (var dst = File.Create(path))
            await src.CopyToAsync(dst);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path),
        });
    }
}
