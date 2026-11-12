using StatusSite;

namespace StatusSite.Tests;

public class MaintenanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly SiteConfig Site = new("Main", "https://example.test");

    [Fact]
    public void IsActive_MatchesSiteAndWindow_EndIsExclusive()
    {
        var windows = new[] { new MaintenanceWindow("Main", Now.AddHours(-1), Now, "Upgrade") };

        Assert.True(Maintenance.IsActive(windows, "Main", Now.AddMinutes(-1)));
        Assert.False(Maintenance.IsActive(windows, "Main", Now));
        Assert.False(Maintenance.IsActive(windows, "Other", Now.AddMinutes(-1)));
    }

    [Fact]
    public void IsActive_AllSites_CoversEverySite()
    {
        var windows = new[] { new MaintenanceWindow("All", Now.AddHours(-1), Now.AddHours(1), "Hosting") };

        Assert.True(Maintenance.IsActive(windows, "Main", Now));
        Assert.True(Maintenance.IsActive(windows, "Other", Now));
    }

    [Fact]
    public void Upcoming_ExcludesFinishedWindows_AndSortsByStart()
    {
        var windows = new[]
        {
            new MaintenanceWindow("Main", Now.AddDays(2), Now.AddDays(2).AddHours(1), "Later"),
            new MaintenanceWindow("Main", Now.AddHours(-3), Now.AddHours(-2), "Finished"),
            new MaintenanceWindow("All", Now.AddHours(-1), Now.AddHours(1), "Now"),
        };

        var upcoming = Maintenance.Upcoming(windows, Now);

        Assert.Equal(new[] { "Now", "Later" }, upcoming.Select(w => w.Message));
    }

    [Fact]
    public void Uptime_ExcludesMaintenanceChecks()
    {
        var history = new[]
        {
            new CheckResult("Main", Now.AddMinutes(-30), true, 200, 100, null),
            new CheckResult("Main", Now.AddMinutes(-20), false, null, 100, "planned", InMaintenance: true),
            new CheckResult("Main", Now.AddMinutes(-10), false, null, 100, "planned", InMaintenance: true),
        };

        var status = Assert.Single(StatusReport.Build(new[] { Site }, history, Now));

        Assert.Equal(100, status.Uptime24h);
    }

    [Fact]
    public void Incidents_IgnoreMaintenanceFailures()
    {
        var history = new[]
        {
            new CheckResult("Main", Now.AddMinutes(-30), true, 200, 100, null),
            new CheckResult("Main", Now.AddMinutes(-20), false, null, 100, "planned", InMaintenance: true),
            new CheckResult("Main", Now.AddMinutes(-10), true, 200, 100, null),
        };

        Assert.Empty(StatusReport.Incidents(new[] { Site }, history));
    }
}
