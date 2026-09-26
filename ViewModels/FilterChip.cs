using CommunityToolkit.Mvvm.ComponentModel;

namespace SuikodenCodex.ViewModels;

public partial class FilterChip : ObservableObject
{
    public FilterChip(string dimension, string value, string label)
    {
        Dimension = dimension;
        Value = value;
        Label = label;
    }

    public string Dimension { get; }
    public string Value { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;
}
