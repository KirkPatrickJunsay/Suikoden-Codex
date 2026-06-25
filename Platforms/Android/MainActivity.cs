using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace SuikodenCodex;

[Activity(Name = "com.codesandchips.suikodencodex.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>Pending app-shortcut action, consumed once the UI is ready.</summary>
    public static string? PendingShortcut;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyDarkSystemBars();
        CaptureShortcut(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        CaptureShortcut(intent);
        // App already running → route immediately.
        if (PendingShortcut is not null)
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                SuikodenCodex.Platforms.Android.ShortcutRouter.ConsumePending);
    }

    private static void CaptureShortcut(Intent? intent)
    {
        var action = intent?.Action;
        if (action is null) return;
        if (action.EndsWith(".SEARCH")) PendingShortcut = "search";
        else if (action.EndsWith(".RANDOM")) PendingShortcut = "random";
        else if (action.EndsWith(".CARDS")) PendingShortcut = "cards";
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplyDarkSystemBars();
    }

    // Fires after the window is fully laid out — wins over MAUI/Shell's own status-bar color.
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) ApplyDarkSystemBars();
    }

    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyDarkSystemBars();
    }

    // Match the dark toolbar: a dark status bar and navigation bar with light icons.
    void ApplyDarkSystemBars()
    {
        if (Window is null) return;
        var dark = Android.Graphics.Color.ParseColor("#161A2E");
        Window.SetStatusBarColor(dark);
        Window.SetNavigationBarColor(dark);

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = false;     // light (white) status-bar icons
            controller.AppearanceLightNavigationBars = false; // light nav-bar icons
        }
    }
}
