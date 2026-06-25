using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class DuelPage : ContentPage
{
    readonly DuelViewModel _vm;

    public DuelPage(DuelViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LockPortrait(true);          // board is designed for portrait
        await _vm.InitializeAsync(); // loads data once + refreshes saved decks each time
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LockPortrait(false);  // restore normal rotation when leaving the duel
    }

    static void LockPortrait(bool on)
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
            activity.RequestedOrientation = on
                ? Android.Content.PM.ScreenOrientation.Portrait
                : Android.Content.PM.ScreenOrientation.Unspecified;
#endif
    }
}
