using StatusSite;

namespace StatusSite.Tests;

public class IncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteConfig Site = new("Main", "https://example.test");

    private static CheckResult Check(double minutesAgo, bool up, string? error = null) =>
        new("Main", Now.AddMinutes(-minutesAgo), up, up ? 200 : 503, 100, up ? null : error ?? "boom");

    [Fact]
    public void Incidents_GroupsConsecutiveFailures_IntoOneIncident()
    {
        var history = new[]
        {
            Check(30, true),
            Check(25, false, "timeout"),
            Check(20, false, "timeout"),
            Check(15, true),
        };

        var incidents = StatusReport.Incidents(new[] { Site }, history);

        var incident = Assert.Single(incidents);
        Assert.Equal(Now.AddMinutes(-25), incident.Start);
        Assert.Equal(Now.AddMinutes(-15), incident.End);
        Assert.Equal("timeout", incident.Cause);
    }

    [Fact]
    public void Incidents_LeavesOngoingIncident_OpenWhenLatestCheckFails()
    {
        var history = new[]
        {
            Check(10, true),
            new CheckResult("Main", Now.AddMinutes(-5), false, 503, 100, null),
        };

        var incident = Assert.Single(StatusReport.Incidents(new[] { Site }, history));

        Assert.Null(incident.End);
        Assert.Equal("HTTP 503", incident.Cause);
    }

    [Fact]
    public void Incidents_ReturnsNothing_WhenAllChecksPass()
    {
        var history = new[] { Check(10, true), Check(5, true) };

        Assert.Empty(StatusReport.Incidents(new[] { Site }, history));
    }

    [Fact]
    public void Incidents_IgnoresSingleFailedCheck_WhenMinimumIsTwo()
    {
        var history = new[]
        {
            Check(20, true),
            Check(15, false, "timeout"),
            Check(10, true),
        };

        Assert.Empty(StatusReport.Incidents(new[] { Site }, history, minimumFailures: 2));
    }

    [Fact]
    public void Incidents_RecordsRun_WhenFailuresReachMinimum()
    {
        var history = new[]
        {
            Check(20, true),
            Check(15, false, "timeout"),
            Check(10, false, "timeout"),
            Check(5, true),
        };

        var incident = Assert.Single(StatusReport.Incidents(new[] { Site }, history, minimumFailures: 2));

        Assert.Equal(Now.AddMinutes(-15), incident.Start);
        Assert.Equal(Now.AddMinutes(-5), incident.End);
    }

    [Fact]
    public void Incidents_ReportsOngoingRun_WhenFailuresReachMinimum()
    {
        var history = new[]
        {
            Check(10, true),
            Check(5, false, "timeout"),
            Check(0, false, "timeout"),
        };

        var incident = Assert.Single(StatusReport.Incidents(new[] { Site }, history, minimumFailures: 2));

        Assert.Null(incident.End);
        Assert.Equal(Now.AddMinutes(-5), incident.Start);
    }

    [Fact]
    public void Incidents_ReturnsNewestFirst()
    {
        var other = new SiteConfig("Other", "https://other.test");
        var history = new[]
        {
            Check(60, false),
            Check(55, true),
            new CheckResult("Other", Now.AddMinutes(-20), false, null, 100, "down"),
            new CheckResult("Other", Now.AddMinutes(-15), true, 200, 100, null),
        };

        var incidents = StatusReport.Incidents(new[] { Site, other }, history);

        Assert.Equal(2, incidents.Count);
        Assert.Equal("Other", incidents[0].Site);
        Assert.Equal("Main", incidents[1].Site);
    }
}
