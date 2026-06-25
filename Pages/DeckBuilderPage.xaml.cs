using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class DeckBuilderPage : ContentPage
{
    readonly DeckBuilderViewModel _vm;
    bool _init;

    public DeckBuilderPage(DeckBuilderViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_init) return;
        _init = true;
        await _vm.InitializeAsync();
    }

    void OnSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;
        // card-shaped tiles (~150 dip wide) regardless of orientation/screen size
        CardGrid.Span = Math.Clamp((int)(Width / 150), 2, 14);
    }
}
