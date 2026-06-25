using Android.Appwidget;
using Android.Content;

namespace SuikodenCodex.Platforms.Android;

/// <summary>Pushes a refresh to the home-screen Stars widget after progress changes.</summary>
public static class WidgetUpdater
{
    public static void Refresh()
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            var mgr = AppWidgetManager.GetInstance(ctx);
            var component = new ComponentName(ctx, Java.Lang.Class.FromType(typeof(StarsWidgetProvider)));
            var ids = mgr?.GetAppWidgetIds(component);
            if (ids is null || ids.Length == 0) return;

            var intent = new Intent(ctx, typeof(StarsWidgetProvider));
            intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);
            ctx.SendBroadcast(intent);
        }
        catch
        {
            // best-effort; widget will refresh on its next period
        }
    }
}
