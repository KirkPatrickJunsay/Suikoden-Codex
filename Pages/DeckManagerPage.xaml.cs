using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class DeckManagerPage : ContentPage
{
    readonly DeckManagerViewModel _vm;

    public DeckManagerPage(DeckManagerViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();   // refresh every time (e.g. returning from the builder)
    }
}
