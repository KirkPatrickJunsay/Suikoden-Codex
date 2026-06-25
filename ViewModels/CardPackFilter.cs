using CommunityToolkit.Mvvm.ComponentModel;

namespace SuikodenCodex.ViewModels;

public partial class CardPackFilter : ObservableObject
{
    public CardPackFilter(string? pack)
    {
        Pack = pack;
        Label = pack ?? "All packs";
    }

    public string? Pack { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}
