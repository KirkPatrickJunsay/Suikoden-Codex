namespace SuikodenCodex.Models;

public class WalkGame
{
    public string Game { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Intro { get; set; } = "";
    public List<WalkChapter> Chapters { get; set; } = new();
}

public class WalkChapter
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Context { get; set; } = "";
    public List<WalkStep> Steps { get; set; } = new();
    public List<int> Recruits { get; set; } = new();   // recruitment.json "num" values
}

public class WalkStep
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "story";   // story | recruit | boss | tip
    public string Text { get; set; } = "";
    public bool Missable { get; set; }
    public string? Link { get; set; }              // codex entry id (optional)
}
