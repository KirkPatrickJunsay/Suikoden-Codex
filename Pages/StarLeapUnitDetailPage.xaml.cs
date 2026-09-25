using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class StarLeapUnitDetailPage : ContentPage
{
    public StarLeapUnitDetailPage(StarLeapUnitDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
