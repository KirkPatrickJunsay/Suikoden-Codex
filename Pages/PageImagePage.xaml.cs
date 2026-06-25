namespace SuikodenCodex.Pages;

[QueryProperty(nameof(Page), "page")]
public partial class PageImagePage : ContentPage
{
    private string? _page;
    public string? Page
    {
        get => _page;
        set { _page = value; _ = LoadAsync(); }
    }

    public PageImagePage()
    {
        InitializeComponent();
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_page)) return;

        var assetPath = $"pages/page_{_page}.jpg";
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            PageImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            await DisplayAlertAsync("Unavailable",
                "The original scan for this page isn't bundled in this build.", "OK");
        }
    }
}
