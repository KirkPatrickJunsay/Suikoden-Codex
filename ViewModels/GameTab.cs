using CommunityToolkit.Mvvm.ComponentModel;

namespace SuikodenCodex.ViewModels;

/// <summary>A selectable game in the Recruitment tab's game selector.</summary>
public partial class GameTab : ObservableObject
{
    public string Game { get; }    // e.g. "Suikoden I" (matches recruitment.json key)
    public string Label { get; }   // short label, e.g. "I"
    public bool Available { get; } // false = "coming soon"

    [ObservableProperty]
    private bool _isSelected;

    public GameTab(string game, string label, bool available)
    {
        Game = game;
        Label = label;
        Available = available;
    }
}
