using System.Text;

namespace SuikodenCodex.StarLeap;

public static class EffectFormatter
{
    public static string Format(SlEffect effect)
    {
        var text = new StringBuilder(effect.Label);
        if (effect.Values.Count > 0)
            text.Append(' ').Append(string.Join(" → ", effect.Values));
        if (effect.Tags.Count > 0)
            text.Append(" · ").Append(string.Join(", ", effect.Tags));
        if (effect.Hits is > 1)
            text.Append(" ×").Append(effect.Hits.Value);
        if (!string.IsNullOrEmpty(effect.Note))
            text.Append(' ').Append(effect.Note);
        return text.ToString();
    }
}
