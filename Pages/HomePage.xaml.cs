using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private bool _checkedOnboarding;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();

        if (!_checkedOnboarding)
        {
            _checkedOnboarding = true;
            if (!Preferences.Get(OnboardingPage.SeenKey, false))
                await Navigation.PushModalAsync(new OnboardingPage());
        }

#if ANDROID
        SuikodenCodex.Platforms.Android.ShortcutRouter.ConsumePending();
#endif
    }
}
