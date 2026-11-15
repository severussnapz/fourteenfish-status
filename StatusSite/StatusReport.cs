namespace StatusSite;

public static class StatusReport
{
    public static double? Uptime(IEnumerable<CheckResult> results)
    {
        var list = results.Where(r => !r.InMaintenance).ToList();
        if (list.Count == 0)
        {
            return null;
        }
        return Math.Round(100.0 * list.Count(r => r.Up) / list.Count, 2);
    }

    public static double? AverageResponseMs(IEnumerable<CheckResult> results)
    {
        var list = results.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(r => r.ElapsedMs), 0);
    }

    // An incident is a run of at least `minimumFailures` consecutive failed checks for one site.
    // It starts at the first failed check and ends at the first successful check after the run,
    // so a single failed check (a one-off blip) is not reported as an incident.
    public static IReadOnlyList<Incident> Incidents(
        IEnumerable<SiteConfig> sites,
        IReadOnlyList<CheckResult> history,
        int minimumFailures = 1)
    {
        var incidents = new List<Incident>();
        foreach (var site in sites)
        {
            var run = new List<CheckResult>();
            foreach (var result in history.Where(r => r.Site == site.Name && !r.InMaintenance).OrderBy(r => r.CheckedAt))
            {
                if (!result.Up)
                {
                    run.Add(result);
                    continue;
                }

                if (run.Count >= minimumFailures)
                {
                    incidents.Add(new Incident(site.Name, run[0].CheckedAt, result.CheckedAt, Cause(run[0])));
                }
                run.Clear();
            }

            if (run.Count >= minimumFailures)
            {
                incidents.Add(new Incident(site.Name, run[0].CheckedAt, null, Cause(run[0])));
            }
        }
        return incidents.OrderByDescending(i => i.Start).ToList();
    }

    private static string Cause(CheckResult failure) =>
        failure.Error ?? $"HTTP {failure.StatusCode}";

    public static IReadOnlyList<SiteStatus> Build(
        IEnumerable<SiteConfig> sites,
        IReadOnlyList<CheckResult> history,
        DateTimeOffset now)
    {
        var last24h = now.AddHours(-24);
        var last30d = now.AddDays(-30);

        return sites.Select(site =>
        {
            var siteHistory = history.Where(r => r.Site == site.Name).ToList();
            var latest = siteHistory.OrderBy(r => r.CheckedAt).LastOrDefault();
            var day = siteHistory.Where(r => r.CheckedAt >= last24h).ToList();
            var month = siteHistory.Where(r => r.CheckedAt >= last30d).ToList();

            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var days = Enumerable.Range(0, 30)
                .Select(i => today.AddDays(i - 29))
                .Select(date => new DayBar(
                    date,
                    Uptime(siteHistory.Where(r => DateOnly.FromDateTime(r.CheckedAt.UtcDateTime) == date))))
                .ToList();

            return new SiteStatus(
                site.Name,
                site.Url,
                latest?.Up,
                latest?.CheckedAt,
                latest?.StatusCode,
                Uptime(day),
                Uptime(month),
                AverageResponseMs(day),
                days);
        }).ToList();
    }
}
