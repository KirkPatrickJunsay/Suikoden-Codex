using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Models;

namespace SuikodenCodex.ViewModels;

/// <summary>A collapsible profile section on the entry detail page.</summary>
public partial class SectionVM : ObservableObject
{
    public string Title { get; }
    public string Body { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public SectionVM(Section s)
    {
        Title = s.Title;
        Body = s.Body;
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
