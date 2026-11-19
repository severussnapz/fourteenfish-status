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
        int minimumFailures = 1,
        DateTimeOffset? since = null)
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
        // Incidents that ended before `since` are dropped; ones still open or ending later are kept.
        return incidents
            .Where(i => since is null || i.End is null || i.End >= since)
            .OrderByDescending(i => i.Start)
            .ToList();
    }

    private static string Cause(CheckResult failure) =>
        failure.Error ?? $"HTTP {failure.StatusCode}";

    // Failed checks that are not part of a run of at least `minimumFailures` are dropped entirely,
    // so one-off blips count neither for nor against uptime. Maintenance checks are ignored as they are for incidents.
    public static IReadOnlyList<CheckResult> WithoutBlips(IEnumerable<CheckResult> results, int minimumFailures)
    {
        var kept = new List<CheckResult>();
        var run = new List<CheckResult>();
        foreach (var result in results.Where(r => !r.InMaintenance).OrderBy(r => r.CheckedAt))
        {
            if (!result.Up)
            {
                run.Add(result);
                continue;
            }

            if (run.Count >= minimumFailures)
            {
                kept.AddRange(run);
            }
            run.Clear();
            kept.Add(result);
        }

        if (run.Count >= minimumFailures)
        {
            kept.AddRange(run);
        }
        return kept;
    }

    public static IReadOnlyList<SiteStatus> Build(
        IEnumerable<SiteConfig> sites,
        IReadOnlyList<CheckResult> history,
        DateTimeOffset now,
        int minimumFailures = 1)
    {
        var last24h = now.AddHours(-24);
        var last30d = now.AddDays(-30);

        return sites.Select(site =>
        {
            var allSiteHistory = history.Where(r => r.Site == site.Name).ToList();
            var latest = allSiteHistory.OrderBy(r => r.CheckedAt).LastOrDefault();
            var siteHistory = WithoutBlips(allSiteHistory, minimumFailures);
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
