using SuikodenCodex.Services;
using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class EntryDetailPage : ContentPage
{
    private readonly EntryDetailViewModel _vm;

    public EntryDetailPage(EntryDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnShare(object? sender, EventArgs e)
    {
        var name = _vm.Entry?.Name ?? "entry";
        await ShareService.ShareElementAsync(ShareFrame, $"{name} — Suikoden Codex", "suikoden-entry.png");
    }
}
