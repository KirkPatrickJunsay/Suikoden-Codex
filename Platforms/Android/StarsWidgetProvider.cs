using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace SuikodenCodex.Platforms.Android;

[BroadcastReceiver(Label = "Suikoden Codex · Stars", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_stars_info")]
public class StarsWidgetProvider : AppWidgetProvider
{
    private const int Total = 540; // 5 games × 108 Stars

    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        foreach (var id in appWidgetIds)
            Render(context, appWidgetManager, id);
    }

    private static void Render(Context ctx, AppWidgetManager mgr, int widgetId)
    {
        int recruited = ReadRecruitedCount(ctx);
        var views = new RemoteViews(ctx.PackageName, Resource.Layout.widget_stars);
        views.SetTextViewText(Resource.Id.widget_count, $"{recruited} / {Total}");
        views.SetProgressBar(Resource.Id.widget_bar, Total, recruited, false);

        var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
        if (launch is not null)
        {
            var pi = PendingIntent.GetActivity(ctx, 0, launch,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            views.SetOnClickPendingIntent(Resource.Id.widget_root, pi);
        }

        mgr.UpdateAppWidget(widgetId, views);
    }

    // Reads the recruited-stars set saved by MAUI Preferences (JSON array of "game#num").
    private static int ReadRecruitedCount(Context ctx)
    {
        try
        {
            var prefs = ctx.GetSharedPreferences(
                $"{ctx.PackageName}.microsoft.maui.essentials.preferences", FileCreationMode.Private);
            var json = prefs?.GetString("recruited_stars", "") ?? "";
            if (string.IsNullOrEmpty(json) || json == "[]") return 0;
            // Each element is a quoted string "game#num"; count quote-pairs.
            int quotes = json.Count(c => c == '"');
            return quotes / 2;
        }
        catch
        {
            return 0;
        }
    }
}
