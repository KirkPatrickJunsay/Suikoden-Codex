using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class CardsPage : ContentPage
{
    private readonly CardsViewModel _vm;

    public CardsPage(CardsViewModel vm)
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
