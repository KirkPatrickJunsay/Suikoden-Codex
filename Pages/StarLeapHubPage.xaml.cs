using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapHubPage : ContentPage
{
    private readonly StarLeapHubViewModel _vm;

    public StarLeapHubPage(StarLeapHubViewModel vm)
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
