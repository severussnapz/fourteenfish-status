namespace StatusSite;

public static class Maintenance
{
    public const string AllSites = "All";

    public static bool IsActive(IEnumerable<MaintenanceWindow> windows, string site, DateTimeOffset at) =>
        windows.Any(w => Applies(w, site) && at >= w.Start && at < w.End);

    public static IReadOnlyList<MaintenanceWindow> Upcoming(IEnumerable<MaintenanceWindow> windows, DateTimeOffset now) =>
        windows.Where(w => w.End > now).OrderBy(w => w.Start).ToList();

    private static bool Applies(MaintenanceWindow window, string site) =>
        window.Site == AllSites || string.Equals(window.Site, site, StringComparison.OrdinalIgnoreCase);
}
