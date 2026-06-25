using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class EntryListPage : ContentPage
{
    public EntryListPage(EntryListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
