using SuikodenCodex.Services;
using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class CardDetailPage : ContentPage
{
    private readonly CardDetailViewModel _vm;

    public CardDetailPage(CardDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private async void OnShare(object? sender, EventArgs e)
    {
        var name = _vm.Card?.Name ?? "card";
        await ShareService.ShareElementAsync(ShareFrame, $"{name} — Suikoden Codex", "suikoden-card.png");
    }
}
