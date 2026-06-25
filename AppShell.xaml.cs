using SuikodenCodex.Pages;

namespace SuikodenCodex;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Routes for detail navigation (not part of the tab bar).
		Routing.RegisterRoute(nameof(EntryDetailPage), typeof(EntryDetailPage));
		Routing.RegisterRoute(nameof(PageImagePage), typeof(PageImagePage));
		Routing.RegisterRoute(nameof(EntryListPage), typeof(EntryListPage));
		Routing.RegisterRoute(nameof(TimelinePage), typeof(TimelinePage));
		Routing.RegisterRoute(nameof(CardDetailPage), typeof(CardDetailPage));
		Routing.RegisterRoute(nameof(ImageViewerPage), typeof(ImageViewerPage));
		Routing.RegisterRoute(nameof(ComparePage), typeof(ComparePage));
		Routing.RegisterRoute(nameof(WorldMapPage), typeof(WorldMapPage));
		Routing.RegisterRoute(nameof(DuelPage), typeof(DuelPage));
		Routing.RegisterRoute(nameof(DeckManagerPage), typeof(DeckManagerPage));
		Routing.RegisterRoute(nameof(DeckBuilderPage), typeof(DeckBuilderPage));
	}
}
