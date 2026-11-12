using StatusSite;

namespace StatusSite.Tests;

public class AdminFormTests
{
    private static readonly SiteConfig[] Sites =
    {
        new("FourteenFish", "https://fourteenfish.com"),
        new("FishBase", "https://fishbase.co.uk"),
    };

    [Fact]
    public void TryCreate_ValidInput_ReturnsUtcWindow()
    {
        var window = AdminForm.TryCreate("FishBase", "2026-11-12T22:00", "2026-11-12T23:30", "Database upgrade", Sites, out var error);

        Assert.Null(error);
        Assert.NotNull(window);
        Assert.Equal("FishBase", window!.Site);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero), window.End);
        Assert.Equal("Database upgrade", window.Message);
    }

    [Fact]
    public void TryCreate_AllSites_IsAccepted()
    {
        var window = AdminForm.TryCreate("All", "2026-11-12T22:00", "2026-11-12T23:00", "Maintenance", Sites, out _);

        Assert.NotNull(window);
        Assert.Equal("All", window!.Site);
    }

    [Fact]
    public void TryCreate_UnknownSite_IsRejected()
    {
        var window = AdminForm.TryCreate("Nope", "2026-11-12T22:00", "2026-11-12T23:00", "x", Sites, out var error);

        Assert.Null(window);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_EndBeforeStart_IsRejected()
    {
        var window = AdminForm.TryCreate("All", "2026-11-12T23:00", "2026-11-12T22:00", "x", Sites, out var error);

        Assert.Null(window);
        Assert.Contains("after", error);
    }

    [Fact]
    public void TryCreate_EmptyMessage_IsRejected()
    {
        var window = AdminForm.TryCreate("All", "2026-11-12T22:00", "2026-11-12T23:00", "   ", Sites, out var error);

        Assert.Null(window);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_MessageTooLong_IsRejected()
    {
        var message = new string('a', AdminForm.MaxMessageLength + 1);

        var window = AdminForm.TryCreate("All", "2026-11-12T22:00", "2026-11-12T23:00", message, Sites, out var error);

        Assert.Null(window);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_BadDate_IsRejected()
    {
        var window = AdminForm.TryCreate("All", "tomorrow", "2026-11-12T23:00", "x", Sites, out var error);

        Assert.Null(window);
        Assert.NotNull(error);
    }

    [Fact]
    public void Render_IncludesFormAndSavedWindowsWithAntiforgeryToken()
    {
        var store = new MaintenanceStore(Path.Combine(Path.GetTempPath(), "render-" + Guid.NewGuid() + ".json"), Array.Empty<MaintenanceWindow>());
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        store.Add(new MaintenanceWindow("FishBase", now.AddHours(1), now.AddHours(2), "Database upgrade"), now);

        var html = AdminPage.Render(Sites, store, "token-123", now);

        Assert.Contains("Schedule planned maintenance", html);
        Assert.Contains("name=\"__RequestVerificationToken\" value=\"token-123\"", html);
        Assert.Contains("Database upgrade", html);
        Assert.Contains("/admin/maintenance/", html);
    }
}

