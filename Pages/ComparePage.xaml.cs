using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class ComparePage : ContentPage
{
    private readonly CompareViewModel _vm;

    public ComparePage(CompareViewModel vm)
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
