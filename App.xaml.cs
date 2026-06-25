using Microsoft.Extensions.DependencyInjection;

namespace SuikodenCodex;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Default the app to dark mode; remember the user's choice across launches.
		var theme = Preferences.Get("app_theme", "Dark");
		UserAppTheme = theme == "Light" ? AppTheme.Light : AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}