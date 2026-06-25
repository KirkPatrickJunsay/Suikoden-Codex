namespace SuikodenCodex.Models;

/// <summary>Serializable snapshot of the user's progress for backup/restore.</summary>
public class BackupData
{
    public string App { get; set; } = "SuikodenCodex";
    public int Version { get; set; } = 1;
    public string ExportedAt { get; set; } = "";
    public List<string> Favorites { get; set; } = new();
    public List<string> Recruited { get; set; } = new();   // keys: "{game}#{num}"
    public List<string> Recent { get; set; } = new();
    public bool SpoilerSafe { get; set; }
}
