using SuikodenCodex.StarLeap;

namespace SuikodenCodex.StarLeap.Tests;

public class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly UpdatePolicy _policy = new(() => Now, TimeSpan.FromHours(24));

    [Fact] public void Due_when_never_checked() => Assert.True(_policy.IsDue(null));
    [Fact] public void Not_due_within_interval() => Assert.False(_policy.IsDue(Now.AddHours(-23)));
    [Fact] public void Due_after_interval() => Assert.True(_policy.IsDue(Now.AddHours(-25)));
}
