using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public sealed class SlUnitRow
{
    public SlUnitRow(SlUnit unit, ImageSource portrait)
    {
        Unit = unit;
        Portrait = portrait;
    }

    public SlUnit Unit { get; }
    public ImageSource Portrait { get; }
    public string Id => Unit.Id;
    public string Name => Unit.Name;
    public string DisplayName => Unit.DisplayName;

    public string Subtitle => string.Join("  •  ",
        new[] { Unit.Rarity, string.Join("/", Unit.Elements), Unit.Role }.Where(s => !string.IsNullOrEmpty(s)));
}
