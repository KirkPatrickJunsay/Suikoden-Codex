using System.Collections.ObjectModel;
using SuikodenCodex.Models;

namespace SuikodenCodex.ViewModels;

/// <summary>A keyed group of entries for the grouped CollectionView (A–Z headers).</summary>
public class EntryGroup : ObservableCollection<CodexEntry>
{
    public string Key { get; }
    public EntryGroup(string key, IEnumerable<CodexEntry> items) : base(items) => Key = key;
}
