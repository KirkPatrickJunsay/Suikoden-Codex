using CommunityToolkit.Mvvm.ComponentModel;
using SuikodenCodex.Models;

namespace SuikodenCodex.ViewModels;

public partial class RecruitItem : ObservableObject
{
    public RecruitChar Data { get; }

    [ObservableProperty]
    private bool _recruited;

    public RecruitItem(RecruitChar data, bool recruited)
    {
        Data = data;
        _recruited = recruited;
    }

    public int Num => Data.Num;
    public string Name => Data.Name;
    public string Method => Data.Method;
    public string? Star => Data.Star;
    public bool HasStar => !string.IsNullOrWhiteSpace(Data.Star);
    public bool HasEntry => !string.IsNullOrEmpty(Data.EntryId);
    public string? EntryId => Data.EntryId;
    public bool MissableHint => Data.MissableHint;
}
