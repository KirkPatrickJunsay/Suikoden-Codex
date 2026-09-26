using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapUnitsPage : ContentPage
{
    private readonly StarLeapUnitsViewModel _vm;

    public StarLeapUnitsPage(StarLeapUnitsViewModel vm)
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
