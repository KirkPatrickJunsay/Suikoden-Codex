namespace SuikodenCodex.StarLeap;

public sealed class UpdatePolicy
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _interval;

    public UpdatePolicy(Func<DateTimeOffset> clock, TimeSpan interval)
    {
        _clock = clock;
        _interval = interval;
    }

    public bool IsDue(DateTimeOffset? lastSuccessfulCheck) =>
        lastSuccessfulCheck is null || _clock() - lastSuccessfulCheck.Value >= _interval;
}
