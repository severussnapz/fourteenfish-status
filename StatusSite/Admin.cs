using System.Globalization;
using System.Net;

namespace StatusSite;

public static class AdminForm
{
    public const int MaxMessageLength = 300;

    private const string LocalTimeFormat = "yyyy-MM-dd'T'HH:mm";

    // Returns null and sets error when the input is invalid. Times are read as UTC.
    public static MaintenanceWindow? TryCreate(
        string? site,
        string? start,
        string? end,
        string? message,
        IEnumerable<SiteConfig> sites,
        out string? error)
    {
        error = null;
        var siteName = (site ?? "").Trim();
        var matchedSite = sites.FirstOrDefault(s => string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));
        if (matchedSite is null && !string.Equals(siteName, Maintenance.AllSites, StringComparison.OrdinalIgnoreCase))
        {
            error = "Choose a site from the list.";
            return null;
        }

        if (!DateTimeOffset.TryParseExact(start, LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startAt)
            || !DateTimeOffset.TryParseExact(end, LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var endAt))
        {
            error = "Enter a valid start and end time.";
            return null;
        }

        if (endAt <= startAt)
        {
            error = "The end time must be after the start time.";
            return null;
        }

        var text = (message ?? "").Trim();
        if (text.Length == 0)
        {
            error = "Enter a message for users.";
            return null;
        }

        if (text.Length > MaxMessageLength)
        {
            error = $"Keep the message under {MaxMessageLength} characters.";
            return null;
        }

        return new MaintenanceWindow(matchedSite?.Name ?? Maintenance.AllSites, startAt, endAt, text);
    }
}

public static class AdminPage
{
    private const string AdminCss = """
        .admin-form { display: grid; gap: .75rem; margin-top: .5rem; }
        .admin-form label { display: grid; gap: .3rem; font-size: .85rem; color: var(--muted); }
        .admin-form input, .admin-form select, .admin-form textarea { font: inherit; color: var(--text); background: var(--bg); border: 1px solid var(--border); border-radius: 6px; padding: .45rem .6rem; }
        .admin-form button, .btn-delete { font: inherit; cursor: pointer; border-radius: 6px; padding: .45rem .9rem; border: 1px solid var(--border); background: var(--panel); color: var(--text); }
        .admin-error { color: var(--down); font-size: .9rem; padding: .6rem 1rem; }
        .admin-notice { color: var(--up); font-size: .9rem; padding: .6rem 1rem; }
        .admin-row { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
        .admin-nav { font-size: .9rem; color: var(--muted); }
        .admin-nav a { color: var(--text); }
        """;

    public static string Render(
        IReadOnlyList<SiteConfig> sites,
        MaintenanceStore maintenance,
        string requestToken,
        DateTimeOffset now,
        string? error = null,
        string? notice = null)
    {
        var siteOptions = string.Join("\n", new[] { Maintenance.AllSites }.Concat(sites.Select(s => s.Name))
            .Select(name => $"""<option value="{WebUtility.HtmlEncode(name)}">{(name == Maintenance.AllSites ? "All sites" : WebUtility.HtmlEncode(name))}</option>"""));

        var upcoming = maintenance.Saved.Where(w => w.End > now).OrderBy(w => w.Start).ToList();
        var savedRows = upcoming.Count == 0
            ? """<div class="empty">No saved maintenance windows.</div>"""
            : string.Join("\n", upcoming.Select(w => $"""
                <div class="incident admin-row">
                  <div class="incident-body">
                    <div class="incident-title">{(w.Site == Maintenance.AllSites ? "All sites" : WebUtility.HtmlEncode(w.Site))}: {WebUtility.HtmlEncode(w.Message)}</div>
                    <div class="incident-meta">{w.Start.UtcDateTime:yyyy-MM-dd HH:mm} to {w.End.UtcDateTime:yyyy-MM-dd HH:mm} UTC</div>
                  </div>
                  <form method="post" action="/admin/maintenance/{w.Id}/delete">
                    <input type="hidden" name="__RequestVerificationToken" value="{WebUtility.HtmlEncode(requestToken)}">
                    <button type="submit" class="btn-delete">Delete</button>
                  </form>
                </div>
                """));

        var configured = maintenance.Configured;
        var configuredRows = configured.Count == 0
            ? """<div class="empty">None.</div>"""
            : string.Join("\n", configured.Select(w => $"""
                <div class="incident">
                  <div class="incident-body">
                    <div class="incident-title">{(w.Site == Maintenance.AllSites ? "All sites" : WebUtility.HtmlEncode(w.Site))}: {WebUtility.HtmlEncode(w.Message)}</div>
                    <div class="incident-meta">{w.Start.UtcDateTime:yyyy-MM-dd HH:mm} to {w.End.UtcDateTime:yyyy-MM-dd HH:mm} UTC · from appsettings.json</div>
                  </div>
                </div>
                """));

        var errorHtml = error is null ? "" : $"""<div class="admin-error">{WebUtility.HtmlEncode(error)}</div>""";
        var noticeHtml = notice is null ? "" : $"""<div class="admin-notice">{WebUtility.HtmlEncode(notice)}</div>""";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="robots" content="noindex">
              <title>Maintenance admin · FourteenFish Status</title>
              <style>{{PageStyle.Css}}{{AdminCss}}</style>
            </head>
            <body>
              <main>
                <header class="top">
                  <img class="brand-logo" src="/fourteenfish-logo.png" alt="FourteenFish" />
                  <span class="crumb">Maintenance admin</span>
                </header>
                <p class="admin-nav"><a href="/">View status page</a> · <a href="/admin/logout">Sign out</a></p>
                {{errorHtml}}
                {{noticeHtml}}

                <section class="panel incidents">
                  <div class="panel-head">
                    <span>Schedule planned maintenance</span>
                    <span class="range">Times in UTC</span>
                  </div>
                  <form method="post" action="/admin/maintenance" class="admin-form" style="padding: 1rem;">
                    <input type="hidden" name="__RequestVerificationToken" value="{{WebUtility.HtmlEncode(requestToken)}}">
                    <label>Site
                      <select name="site" required>{{siteOptions}}</select>
                    </label>
                    <label>Start (UTC)
                      <input type="datetime-local" name="start" required>
                    </label>
                    <label>End (UTC)
                      <input type="datetime-local" name="end" required>
                    </label>
                    <label>Message for users (up to {{AdminForm.MaxMessageLength}} characters)
                      <textarea name="message" rows="3" maxlength="{{AdminForm.MaxMessageLength}}" required></textarea>
                    </label>
                    <div><button type="submit">Schedule maintenance</button></div>
                  </form>
                </section>

                <section class="panel incidents">
                  <div class="panel-head">
                    <span>Upcoming maintenance</span>
                    <span class="range">Saved</span>
                  </div>
                  {{savedRows}}
                </section>

                <section class="panel incidents">
                  <div class="panel-head">
                    <span>Configured in appsettings.json</span>
                    <span class="range">Read only</span>
                  </div>
                  {{configuredRows}}
                </section>
              </main>
            </body>
            </html>
            """;
    }
}
