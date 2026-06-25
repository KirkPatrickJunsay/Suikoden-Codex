namespace SuikodenCodex.Pages;

/// <summary>Fullscreen pinch/double-tap zoom viewer for any bundled image (cards, portraits, art).</summary>
[QueryProperty(nameof(ImagePath), "img")]
public partial class ImageViewerPage : ContentPage
{
    private double _current = 1, _start = 1;
    private double _xOffset = 0, _yOffset = 0;

    public ImageViewerPage()
    {
        InitializeComponent();
    }

    public string? ImagePath
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                Img.Source = value;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
            _start = _current;
        else if (e.Status == GestureStatus.Running)
        {
            _current = Math.Clamp(_start + (e.Scale - 1) * _start, 1, 6);
            Img.Scale = _current;
        }
        else if (e.Status == GestureStatus.Completed && _current <= 1)
            ResetTransform();
    }

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_current <= 1) return; // only pan while zoomed
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                Img.TranslationX = _xOffset + e.TotalX;
                Img.TranslationY = _yOffset + e.TotalY;
                break;
            case GestureStatus.Completed:
                _xOffset = Img.TranslationX;
                _yOffset = Img.TranslationY;
                break;
        }
    }

    private void OnDoubleTap(object? sender, TappedEventArgs e)
    {
        if (_current > 1)
        {
            _current = 1;
            ResetTransform();
        }
        else
        {
            _current = 3;
            Img.Scale = 3;
        }
    }

    private void ResetTransform()
    {
        _current = 1;
        _xOffset = _yOffset = 0;
        Img.Scale = 1;
        Img.TranslationX = 0;
        Img.TranslationY = 0;
    }

    private async void OnTapClose(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
