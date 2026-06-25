namespace SuikodenCodex.ViewModels;

/// <summary>Per-game recruitment progress shown on the Home dashboard.</summary>
public class GameProgress
{
    public string Game { get; init; } = "";   // e.g. "Suikoden II"
    public int Recruited { get; init; }
    public int Total { get; init; }
    public double Progress => Total == 0 ? 0 : (double)Recruited / Total;
    public string CountLabel => $"{Recruited} / {Total}";
}
