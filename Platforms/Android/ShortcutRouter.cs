using Microsoft.Extensions.DependencyInjection;
using SuikodenCodex.Services;

namespace SuikodenCodex.Platforms.Android;

/// <summary>Routes a pending app-shortcut action to the right page once Shell is ready.</summary>
public static class ShortcutRouter
{
    public static async void ConsumePending()
    {
        var action = MainActivity.PendingShortcut;
        if (action is null) return;
        MainActivity.PendingShortcut = null;

        var shell = Shell.Current;
        if (shell is null) return;

        try
        {
            switch (action)
            {
                case "search":
                    await shell.GoToAsync("//browse");
                    break;
                case "cards":
                    await shell.GoToAsync("//cards");
                    break;
                case "random":
                    var data = IPlatformApplication.Current?.Services.GetService<CodexData>();
                    if (data is not null)
                    {
                        await data.EnsureLoadedAsync();
                        var e = data.Random();
                        if (e is not null)
                            await shell.GoToAsync($"EntryDetailPage?id={e.Id}");
                    }
                    break;
            }
        }
        catch
        {
            // ignore navigation races
        }
    }
}
