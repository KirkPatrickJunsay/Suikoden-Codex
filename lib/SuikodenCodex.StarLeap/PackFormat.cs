using System.Text.Json;

namespace SuikodenCodex.StarLeap;

public static class PackFormat
{
    public const int SupportedSchema = 1;
    public const string ManifestFile = "manifest.json";
    public const string UnitsFile = "units.json";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class PackException : Exception
{
    public PackException(string message, Exception? inner = null) : base(message, inner) { }
}
