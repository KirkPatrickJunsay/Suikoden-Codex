using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;

namespace SuikodenCodex.ViewModels;

[QueryProperty(nameof(CardId), "id")]
public partial class CardDetailViewModel : ObservableObject
{
    private readonly CardData _data;

    [ObservableProperty] private CardEntry? _card;

    public CardDetailViewModel(CardData data) => _data = data;

    [RelayCommand]
    private async Task OpenImage()
    {
        if (Card?.ImageName is { } img)
            await Shell.Current.GoToAsync($"{nameof(ImageViewerPage)}?img={img}");
    }

    private string? _cardId;
    public string? CardId
    {
        get => _cardId;
        set { _cardId = value; Load(); }
    }

    private async void Load()
    {
        await _data.EnsureLoadedAsync();
        if (!string.IsNullOrEmpty(_cardId))
            Card = _data.GetById(_cardId);
    }
}
