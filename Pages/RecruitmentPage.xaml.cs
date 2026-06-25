using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class RecruitmentPage : ContentPage
{
    private readonly RecruitmentViewModel _vm;

    public RecruitmentPage(RecruitmentViewModel vm)
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
