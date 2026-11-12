using StatusSite;

namespace StatusSite.Tests;

public class MaintenanceStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "maint-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string FilePath => Path.Combine(_dir, "maintenance.json");

    [Fact]
    public void Add_PersistsAndReloads()
    {
        var store = new MaintenanceStore(FilePath, Array.Empty<MaintenanceWindow>());
        var saved = store.Add(new MaintenanceWindow("All", Now.AddHours(1), Now.AddHours(2), "Upgrade"), Now);

        var reloaded = new MaintenanceStore(FilePath, Array.Empty<MaintenanceWindow>());

        var window = Assert.Single(reloaded.Saved);
        Assert.Equal(saved.Id, window.Id);
        Assert.Equal("Upgrade", window.Message);
    }

    [Fact]
    public void Remove_DeletesWindowAndPersists()
    {
        var store = new MaintenanceStore(FilePath, Array.Empty<MaintenanceWindow>());
        var saved = store.Add(new MaintenanceWindow("All", Now.AddHours(1), Now.AddHours(2), "Upgrade"), Now);

        Assert.True(store.Remove(saved.Id));
        Assert.False(store.Remove(saved.Id));

        Assert.Empty(new MaintenanceStore(FilePath, Array.Empty<MaintenanceWindow>()).Saved);
    }

    [Fact]
    public void All_IncludesConfiguredAndSavedWindows()
    {
        var configured = new[] { new MaintenanceWindow("FFARMS", Now, Now.AddHours(1), "Configured") };
        var store = new MaintenanceStore(FilePath, configured);
        store.Add(new MaintenanceWindow("All", Now, Now.AddHours(1), "Saved"), Now);

        var all = store.All();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, w => w.Message == "Configured");
        Assert.Contains(all, w => w.Message == "Saved");
    }

    [Fact]
    public void Add_PrunesWindowsEndedMoreThan30DaysAgo()
    {
        var store = new MaintenanceStore(FilePath, Array.Empty<MaintenanceWindow>());
        store.Add(new MaintenanceWindow("All", Now.AddDays(-40), Now.AddDays(-39), "Old"), Now.AddDays(-39));

        store.Add(new MaintenanceWindow("All", Now.AddHours(1), Now.AddHours(2), "New"), Now);

        var window = Assert.Single(store.Saved);
        Assert.Equal("New", window.Message);
    }
}
