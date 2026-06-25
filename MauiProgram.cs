using Microsoft.Extensions.Logging;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.ViewModels;

namespace SuikodenCodex;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Services (singletons — shared dataset + persisted user state)
		builder.Services.AddSingleton<CodexData>();
		builder.Services.AddSingleton<CardData>();
		builder.Services.AddSingleton<DeckStore>();
		builder.Services.AddSingleton<UserState>();

		// ViewModels
		builder.Services.AddSingleton<HomeViewModel>();
		builder.Services.AddSingleton<BrowseViewModel>();
		builder.Services.AddSingleton<RecruitmentViewModel>();
		builder.Services.AddSingleton<MoreViewModel>();
		builder.Services.AddTransient<EntryDetailViewModel>();
		builder.Services.AddTransient<EntryListViewModel>();
		builder.Services.AddTransient<TimelineViewModel>();
		builder.Services.AddSingleton<CardsViewModel>();
		builder.Services.AddTransient<CardDetailViewModel>();
		builder.Services.AddTransient<CompareViewModel>();
		builder.Services.AddTransient<WorldMapViewModel>();
		builder.Services.AddTransient<DuelViewModel>();
		builder.Services.AddTransient<DeckBuilderViewModel>();
		builder.Services.AddTransient<DeckManagerViewModel>();

		// Pages
		builder.Services.AddSingleton<HomePage>();
		builder.Services.AddSingleton<BrowsePage>();
		builder.Services.AddSingleton<RecruitmentPage>();
		builder.Services.AddSingleton<MorePage>();
		builder.Services.AddTransient<EntryDetailPage>();
		builder.Services.AddTransient<PageImagePage>();
		builder.Services.AddTransient<EntryListPage>();
		builder.Services.AddTransient<TimelinePage>();
		builder.Services.AddSingleton<CardsPage>();
		builder.Services.AddTransient<CardDetailPage>();
		builder.Services.AddTransient<ComparePage>();
		builder.Services.AddTransient<WorldMapPage>();
		builder.Services.AddTransient<DuelPage>();
		builder.Services.AddTransient<DeckBuilderPage>();
		builder.Services.AddTransient<DeckManagerPage>();
		builder.Services.AddTransient<ImageViewerPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
