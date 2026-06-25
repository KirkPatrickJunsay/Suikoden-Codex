using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class TimelinePage : ContentPage
{
    private readonly TimelineViewModel _vm;

    public TimelinePage(TimelineViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }

    private async void OnEventSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TimelineItem item)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            await _vm.OpenEntryCommand.ExecuteAsync(item);
        }
    }
}
