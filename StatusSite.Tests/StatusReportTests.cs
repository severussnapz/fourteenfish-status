using StatusSite;

namespace StatusSite.Tests;

public class StatusReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static CheckResult Check(string site, double hoursAgo, bool up, long ms = 100) =>
        new(site, Now.AddHours(-hoursAgo), up, up ? 200 : 503, ms, up ? null : "boom");

    [Fact]
    public void Uptime_ReturnsNull_WhenNoResults()
    {
        Assert.Null(StatusReport.Uptime(Array.Empty<CheckResult>()));
    }

    [Fact]
    public void Uptime_ReturnsPercentageOfUpChecks()
    {
        var results = new[] { Check("a", 1, true), Check("a", 2, true), Check("a", 3, true), Check("a", 4, false) };

        Assert.Equal(75.0, StatusReport.Uptime(results));
    }

    [Fact]
    public void Build_ReportsLatestStateAndWindows()
    {
        var sites = new[] { new SiteConfig("FishBase", "https://fishbase.co.uk") };
        var history = new[]
        {
            Check("FishBase", 40, false),
            Check("FishBase", 10, true, 300),
            Check("FishBase", 0.5, true, 100),
        };

        var status = StatusReport.Build(sites, history, Now).Single();

        Assert.True(status.Up);
        Assert.Equal(Now.AddMinutes(-30), status.CheckedAt);
        Assert.Equal(200, status.StatusCode);
        Assert.Equal(100.0, status.Uptime24h);
        Assert.Equal(66.67, status.Uptime30d);
        Assert.Equal(200.0, status.AvgResponseMs24h);
    }

    [Fact]
    public void Build_ReturnsEmptyStatus_ForSiteWithoutHistory()
    {
        var sites = new[] { new SiteConfig("ffarms", "https://ffarms.co.uk") };

        var status = StatusReport.Build(sites, Array.Empty<CheckResult>(), Now).Single();

        Assert.Null(status.Up);
        Assert.Null(status.Uptime24h);
        Assert.Null(status.AvgResponseMs24h);
    }

    [Fact]
    public void Build_ReturnsThirtyDailyBars_EndingToday()
    {
        var sites = new[] { new SiteConfig("FishBase", "https://fishbase.co.uk") };
        var history = new[]
        {
            Check("FishBase", 1, true),
            Check("FishBase", 2, false),
            Check("FishBase", 26, true),
        };

        var days = StatusReport.Build(sites, history, Now).Single().Days;

        Assert.Equal(30, days.Count);
        Assert.Equal(DateOnly.FromDateTime(Now.UtcDateTime), days[^1].Date);
        Assert.Equal(50.0, days[^1].Uptime);
        Assert.Null(days[0].Uptime);
    }
}
