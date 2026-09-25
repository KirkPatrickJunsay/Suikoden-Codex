using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuikodenCodex.Pages;
using SuikodenCodex.Services;
using SuikodenCodex.StarLeap;

namespace SuikodenCodex.ViewModels;

public sealed record InfoRow(string Label, string Value);

public sealed record StatRow(string Label, string Level1, string Max);

public sealed class SkillVM
{
    public SkillVM(SlSkill skill)
    {
        Slot = skill.Slot.ToUpperInvariant();
        Name = skill.Name;
        Meta = string.Join("  •  ", new[]
        {
            skill.Target,
            skill.Uses,
            skill.Rune is null ? null : $"Rune: {skill.Rune}",
            skill.Trigger,
        }.Where(s => !string.IsNullOrEmpty(s)));
        Description = skill.Description ?? "";
        Effects = skill.Effects.Select(EffectFormatter.Format).ToList();
    }

    public string Slot { get; }
    public string Name { get; }
    public string Meta { get; }
    public bool HasMeta => Meta.Length > 0;
    public string Description { get; }
    public bool HasDescription => Description.Length > 0;
    public List<string> Effects { get; }
}

[QueryProperty(nameof(UnitId), "id")]
public partial class StarLeapUnitDetailViewModel : ObservableObject
{
    private readonly StarLeapData _data;
    private string? _unitId;

    public StarLeapUnitDetailViewModel(StarLeapData data) => _data = data;

    public ObservableCollection<InfoRow> Info { get; } = new();
    public ObservableCollection<StatRow> Stats { get; } = new();
    public ObservableCollection<SkillVM> Kit { get; } = new();
    public ObservableCollection<SlUnitRow> OtherVersions { get; } = new();
    public string Credit => StarLeapData.Credit;

    [ObservableProperty]
    private SlUnit? _unit;

    [ObservableProperty]
    private ImageSource? _portrait;

    [ObservableProperty]
    private string _japaneseName = "";

    [ObservableProperty]
    private string _badges = "";

    [ObservableProperty]
    private bool _hasStats;

    [ObservableProperty]
    private string _trainingText = "";

    [ObservableProperty]
    private bool _hasTraining;

    [ObservableProperty]
    private string _weaponText = "";

    [ObservableProperty]
    private bool _hasWeapon;

    [ObservableProperty]
    private bool _hasKit;

    [ObservableProperty]
    private bool _hasOtherVersions;

    [ObservableProperty]
    private string? _classicEntryId;

    [ObservableProperty]
    private bool _hasClassic;

    public string? UnitId
    {
        get => _unitId;
        set
        {
            _unitId = value;
            Load();
        }
    }

    [RelayCommand]
    private Task OpenVersion(SlUnitRow? row) =>
        row is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(StarLeapUnitDetailPage)}?id={row.Id}");

    [RelayCommand]
    private Task OpenClassic() =>
        ClassicEntryId is null ? Task.CompletedTask : Shell.Current.GoToAsync($"{nameof(EntryDetailPage)}?id={ClassicEntryId}");

    [RelayCommand]
    private async Task OpenWiki()
    {
        if (Unit?.WikiUrl is { Length: > 0 } url)
            await Launcher.OpenAsync(url);
    }

    private async void Load()
    {
        await _data.EnsureLoadedAsync();
        if (string.IsNullOrEmpty(_unitId) || _data.GetUnit(_unitId) is not { } unit)
            return;

        Unit = unit;
        Portrait = _data.Portrait(unit);
        JapaneseName = string.Join("  ", new[] { unit.NameJp, unit.TitleJp }.Where(s => !string.IsNullOrEmpty(s)));
        Badges = string.Join("  •  ", new[]
        {
            unit.Rarity,
            string.Join("/", unit.Elements),
            unit.Role,
            string.Join("/", unit.Weapons),
        }.Where(s => !string.IsNullOrEmpty(s)));

        Info.Clear();
        AddInfo("Obtained", unit.Obtained);
        AddInfo("Released", unit.Released);
        AddInfo("From", unit.Origin);
        AddInfo("Voice", unit.Voice);
        AddInfo("Illustration", unit.Illustration);

        Stats.Clear();
        if (unit.Stats is { } s)
        {
            AddRange("HP", s.Hp);
            AddRange("PATK", s.Patk);
            AddRange("MATK", s.Matk);
            AddRange("PDEF", s.Pdef);
            AddRange("MDEF", s.Mdef);
            AddSingle("AGI", s.Agi);
            AddSingle("HIT", s.Hit);
            AddSingle("DODGE", s.Dodge);
            TrainingText = string.Join("  ·  ", s.Training.Select(t => $"{t.Key.ToUpperInvariant()} +{t.Value}"));
        }
        else
        {
            TrainingText = "";
        }
        HasStats = Stats.Count > 0;
        HasTraining = TrainingText.Length > 0;

        WeaponText = unit.Weapon is { } w
            ? $"{w.Type}{(string.IsNullOrEmpty(w.Growth) ? "" : $" ({w.Growth})")}: {string.Join(" → ", w.Names)}"
            : "";
        HasWeapon = WeaponText.Length > 0;

        Kit.Clear();
        foreach (var skill in unit.Kit)
            Kit.Add(new SkillVM(skill));
        HasKit = Kit.Count > 0;

        OtherVersions.Clear();
        foreach (var other in _data.OtherVersionsOf(unit))
            OtherVersions.Add(new SlUnitRow(other, _data.Portrait(other)));
        HasOtherVersions = OtherVersions.Count > 0;

        ClassicEntryId = await _data.ClassicEntryIdAsync(unit);
        HasClassic = ClassicEntryId is not null;
    }

    private void AddInfo(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            Info.Add(new InfoRow(label, value));
    }

    private void AddRange(string label, List<int>? values)
    {
        if (values is { Count: 2 })
            Stats.Add(new StatRow(label, values[0].ToString(), values[1].ToString()));
    }

    private void AddSingle(string label, int? value)
    {
        if (value is { } v)
            Stats.Add(new StatRow(label, v.ToString(), v.ToString()));
    }
}
