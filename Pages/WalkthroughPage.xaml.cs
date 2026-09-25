using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class WalkthroughPage : ContentPage
{
    readonly WalkthroughViewModel _vm;

    public WalkthroughPage(WalkthroughViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
