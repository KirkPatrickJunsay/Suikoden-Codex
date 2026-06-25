using CommunityToolkit.Mvvm.ComponentModel;

namespace SuikodenCodex.ViewModels;

/// <summary>A selectable game chip for the Codex browse filter. Token null = "All".</summary>
public partial class GameFilter : ObservableObject
{
    public string? Token { get; }   // roman numeral, e.g. "II"; null = All
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    public GameFilter(string? token)
    {
        Token = token;
        Label = token ?? "All";
    }
}
