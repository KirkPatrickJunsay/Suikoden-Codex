using SuikodenCodex.ViewModels;

namespace SuikodenCodex.Pages;

public partial class WorldMapPage : ContentPage
{
    const double ContentW = 1200, ContentH = 1960;
    const double MaxZoomRatio = 5.0;   // deepest zoom = 5x the fit scale

    readonly WorldMapViewModel _vm;

    double _fitScale = 0.3;
    double _maxScale = 1.5;
    double _scale = 0.3;
    double _tx, _ty;            // committed translation (top-left anchored)

    double _startScale;         // scale at pinch start
    bool _pinching;

    double _panBaseX, _panBaseY;
    int _unlockToken;
    bool _initialized;

    public WorldMapPage(WorldMapViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        MapContent.AnchorX = 0;
        MapContent.AnchorY = 0;
    }

    void OnViewportSizeChanged(object sender, EventArgs e)
    {
        if (Viewport.Width <= 0 || Viewport.Height <= 0) return;

        _fitScale = Math.Min(Viewport.Width / ContentW, Viewport.Height / ContentH);
        _maxScale = _fitScale * MaxZoomRatio;

        if (!_initialized)
        {
            _initialized = true;
            _scale = _fitScale;
            ApplyTransform(center: true);
            UpdateLod();
        }
        else
        {
            _scale = Math.Clamp(_scale, _fitScale, _maxScale);
            ApplyTransform();
        }
    }

    void ApplyTransform(bool center = false, bool updatePins = true)
    {
        double scaledW = ContentW * _scale, scaledH = ContentH * _scale;

        if (center)
        {
            _tx = scaledW <= Viewport.Width ? (Viewport.Width - scaledW) / 2 : 0;
            _ty = scaledH <= Viewport.Height ? (Viewport.Height - scaledH) / 2 : 0;
        }

        ClampTranslation();

        MapContent.Scale = _scale;
        MapContent.TranslationX = _tx;
        MapContent.TranslationY = _ty;

        // Updating PinScale rebinds the counter-scale on every pin, which is too heavy
        // to do per pinch-frame. During a pinch the pins simply ride the map's scale;
        // we snap them back to a constant on-screen size when the gesture ends.
        if (updatePins)
            _vm.PinScale = _scale > 0 ? 1.0 / _scale : 1;
    }

    void ClampTranslation()
    {
        double scaledW = ContentW * _scale, scaledH = ContentH * _scale;
        _tx = scaledW <= Viewport.Width
            ? (Viewport.Width - scaledW) / 2
            : Math.Clamp(_tx, Viewport.Width - scaledW, 0);
        _ty = scaledH <= Viewport.Height
            ? (Viewport.Height - scaledH) / 2
            : Math.Clamp(_ty, Viewport.Height - scaledH, 0);
    }

    void UpdateLod()
    {
        double r = _fitScale > 0 ? _scale / _fitScale : 1;
        int d = r < 1.7 ? 0 : (r < 3.6 ? 1 : 2);   // 0 Nations, 1 Towns, 2 All
        if (_vm.DetailIndex != d) _vm.DetailIndex = d;
    }

    void OnPinch(object sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinching = true;
                _vm.InteractionLocked = true;   // don't let finger contact open a pin
                _startScale = _scale;
                break;

            case GestureStatus.Running:
                if (e.Scale <= 0) break;
                double newScale = Math.Clamp(_startScale * e.Scale, _fitScale, _maxScale);
                // ScaleOrigin is relative to the viewport (the gesture surface). Keep the
                // content point currently under the fingers pinned to that screen spot.
                double fx = e.ScaleOrigin.X * Viewport.Width;
                double fy = e.ScaleOrigin.Y * Viewport.Height;
                double contentX = (fx - _tx) / _scale;
                double contentY = (fy - _ty) / _scale;
                _scale = newScale;
                _tx = fx - contentX * newScale;
                _ty = fy - contentY * newScale;
                ApplyTransform(updatePins: false);   // pins ride the scale; snap at end
                UpdateLod();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _pinching = false;
                _vm.PinScale = _scale > 0 ? 1.0 / _scale : 1;   // snap labels to constant size
                UnlockSoon();
                break;
        }
    }

    void OnPan(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panBaseX = _tx;
                _panBaseY = _ty;
                break;

            case GestureStatus.Running:
                if (_pinching)
                {
                    // Pinch owns the gesture. TotalX keeps accumulating while we ignore
                    // pan, so keep the base in sync with it — that way resuming pan after
                    // the pinch ends continues smoothly instead of snapping by the total.
                    _panBaseX = _tx - e.TotalX;
                    _panBaseY = _ty - e.TotalY;
                    break;
                }
                _tx = _panBaseX + e.TotalX;
                _ty = _panBaseY + e.TotalY;
                ClampTranslation();
                MapContent.TranslationX = _tx;
                MapContent.TranslationY = _ty;
                break;
        }
    }

    void OnSingleTap(object sender, TappedEventArgs e)
    {
        if (_vm.InteractionLocked) return;

        Point? p = e.GetPosition(Viewport);
        if (p is null) return;

        // Pins render at a constant on-screen size (counter-scaled), so a pin centred at
        // content (X+W/2, Y+H/2) sits at screen (centre*scale + translation) and is W×H wide.
        for (int i = _vm.Pins.Count - 1; i >= 0; i--)   // last drawn = topmost
        {
            var pin = _vm.Pins[i];
            double sx = (pin.X + pin.W / 2) * _scale + _tx;
            double sy = (pin.Y + pin.H / 2) * _scale + _ty;
            if (Math.Abs(p.Value.X - sx) <= pin.W / 2 + 8 &&
                Math.Abs(p.Value.Y - sy) <= pin.H / 2 + 8)
            {
                _vm.OpenCommand.Execute(pin.Id);
                return;
            }
        }
    }

    void OnZoomIn(object sender, EventArgs e) => ZoomBy(1.5);
    void OnZoomOut(object sender, EventArgs e) => ZoomBy(1 / 1.5);

    void OnZoomFit(object sender, EventArgs e)
    {
        _scale = _fitScale;
        ApplyTransform(center: true);
        UpdateLod();
    }

    // Zoom about the centre of the viewport by a multiplicative factor.
    void ZoomBy(double factor)
    {
        if (!_initialized) return;
        double newScale = Math.Clamp(_scale * factor, _fitScale, _maxScale);
        double cx = Viewport.Width / 2, cy = Viewport.Height / 2;
        double contentX = (cx - _tx) / _scale;
        double contentY = (cy - _ty) / _scale;
        _scale = newScale;
        _tx = cx - contentX * newScale;
        _ty = cy - contentY * newScale;
        ApplyTransform();
        UpdateLod();
    }

    // Release the tap lock a moment after a pinch ends, so the finger-up that
    // concludes the pinch can't fall through and open a pin.
    void UnlockSoon()
    {
        int token = ++_unlockToken;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(280), () =>
        {
            if (token == _unlockToken)
                _vm.InteractionLocked = false;
        });
    }
}
