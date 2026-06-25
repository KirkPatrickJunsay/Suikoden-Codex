using CommunityToolkit.Mvvm.ComponentModel;
using SuikodenCodex.Models;

namespace SuikodenCodex.ViewModels;

/// <summary>A selectable category chip for the browse filter bar. Null = "All".</summary>
public partial class CategoryFilter : ObservableObject
{
    public EntryCategory? Category { get; }
    public string Label { get; }
    public string Glyph { get; }

    [ObservableProperty]
    private bool _isSelected;

    public CategoryFilter(EntryCategory? category)
    {
        Category = category;
        Label = category?.Display() ?? "All";
        Glyph = category?.Glyph() ?? "📚";
    }
}
