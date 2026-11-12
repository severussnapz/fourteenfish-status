using StatusSite;

namespace StatusSite.Tests;

public class AlertTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static CheckResult Result(bool up) =>
        new("FishBase", Now, up, up ? 200 : 503, 100, up ? null : "boom");

    [Fact]
    public void Alerts_OnlyAfterConfiguredConsecutiveFailures()
    {
        var tracker = new AlertTracker(2);

        Assert.Null(tracker.Evaluate(Result(false)));
        var alert = tracker.Evaluate(Result(false));

        Assert.NotNull(alert);
        Assert.Equal(AlertKind.SiteDown, alert!.Kind);
    }

    [Fact]
    public void DoesNotRepeatDownAlert_WhileSiteStaysDown()
    {
        var tracker = new AlertTracker(1);

        Assert.Equal(AlertKind.SiteDown, tracker.Evaluate(Result(false))!.Kind);
        Assert.Null(tracker.Evaluate(Result(false)));
        Assert.Null(tracker.Evaluate(Result(false)));
    }

    [Fact]
    public void SendsRecoveryAlert_WhenSiteComesBackUp()
    {
        var tracker = new AlertTracker(1);
        tracker.Evaluate(Result(false));

        var alert = tracker.Evaluate(Result(true));

        Assert.NotNull(alert);
        Assert.Equal(AlertKind.SiteRecovered, alert!.Kind);
        Assert.Null(tracker.Evaluate(Result(true)));
    }

    [Fact]
    public void ResetsFailureCount_OnSuccessfulCheck()
    {
        var tracker = new AlertTracker(2);
        tracker.Evaluate(Result(false));
        tracker.Evaluate(Result(true));

        Assert.Null(tracker.Evaluate(Result(false)));
    }

    [Fact]
    public void Compose_DescribesDownSite()
    {
        var (subject, body) = AlertService.Compose(new AlertEvent(AlertKind.SiteDown, Result(false)));

        Assert.Equal("[DOWN] FishBase is down", subject);
        Assert.Contains("Error: boom", body);
    }

    [Fact]
    public void Compose_DescribesRecoveredSite()
    {
        var (subject, _) = AlertService.Compose(new AlertEvent(AlertKind.SiteRecovered, Result(true)));

        Assert.Equal("[RECOVERED] FishBase is back up", subject);
    }
}
