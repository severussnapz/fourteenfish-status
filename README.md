# FourteenFish Status

A standalone status page that checks the uptime of the FourteenFish websites:

- fourteenfish.com
- fishbase.co.uk
- ffarms.co.uk

It polls each site every 5 minutes, keeps 30 days of history, and serves:

- `GET /` – HTML status page (auto-refreshes every 60 seconds)
- `GET /api/status` – JSON with latest state, 24h and 30d uptime, and average response time

A site is **up** when it responds with an HTTP status below 500.

## How it works

**Checks.** When the app starts it checks all sites straight away, then repeats every `IntervalMinutes` (5). Each check sends a GET request to the site's URL with a `TimeoutSeconds` (60) limit. A check is **down** if the request fails or times out, if the status is 500 or higher, or if `Expect` is set and the text is missing from the body. Otherwise it is **up**. Each check is saved as one line in the history file, and the sites are checked in parallel.

**History.** Results are written to `HistoryFile` (`history.jsonl`) and reloaded on startup. Results older than `RetentionDays` (30) are removed, and the file is rewritten when that happens.

**Uptime.** Uptime is the percentage of up checks. The 24-hour and 30-day figures use the last 24 hours and 30 days of checks. Each day bar on the page shows the uptime for one UTC day. A day with no checks is shown empty. The average response time covers the last 24 hours. Single failed checks (see `FailuresBeforeIncident` below) are left out of the uptime figures, so one-off blips don't lower them.

**Incidents.** A run of consecutive down checks for one site forms one incident, but only once the run reaches `FailuresBeforeIncident` checks (2 by default). A single failed check is ignored, so one-off blips don't appear in the log. The incident starts at the first failed check in the run and ends at the first successful check after it. An incident still open is shown as ongoing. Incidents are listed on the page and at `GET /api/incidents`.

**Alerts.** Each site has a failure counter, which is kept in memory. When the counter reaches `FailuresBeforeAlert` (2), a DOWN email is sent. A recovery email is sent on the first up check after that. Alerts are not repeated while the site stays down. The counter resets when the app restarts, so a site that is still down after a restart sends a new DOWN email once it has failed the threshold again.

**Maintenance.** Checks inside a maintenance window are still recorded, but they are left out of uptime, incidents, and alerts (see [Scheduled maintenance](#scheduled-maintenance)).

**Storage.** Everything is stored in the history file. On Render this file must be on the persistent disk, or it is lost on every restart (see [Deploying to Render](#deploying-to-render-starter-plan)).

**Pingdom.** A site with `PingdomUrl` set shows Pingdom's 7-day uptime and average response time under its day bars, as an independent measurement from outside. Under that it shows a blue filled line chart of the average response time per day, with date labels and a millisecond scale, and an expandable "Response time by country" list. Hover or focus a chart point for its date and value. On narrow screens the chart scrolls horizontally to keep labels readable. The figures are read from the Pingdom public report page every 15 minutes, kept in memory, and available at `GET /api/pingdom`. They are shown alongside our own figures and are not used for incidents or alerts. If Pingdom is unreachable or changes its page layout, the line is simply left out.

## Running

Requires the .NET 8 SDK.

```bash
dotnet run --project StatusSite
```

Then open http://localhost:5000 (or the URL shown in the console).

## Configuration

Settings live in `StatusSite/appsettings.json` under `Monitor`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `IntervalMinutes` | `5` | How often to check each site |
| `TimeoutSeconds` | `60` | How long a check can take before the site counts as down |
| `RetentionDays` | `30` | How long history is kept |
| `FailuresBeforeIncident` | `2` | Consecutive failed checks before a failure counts as an incident and in uptime |
| `HistoryFile` | `history.jsonl` | Where check results are stored |
| `Sites` | three FourteenFish sites | Sites to monitor (`Name`, `Url`, optional `Expect`, optional `PingdomUrl`) |
| `Maintenance` | `[]` | Planned maintenance windows (see below) |

### Content checks

A status code alone can miss broken pages, such as a maintenance or error page that still returns 200. Set `Expect` on a site to require some text in the page body:

```json
{ "Name": "FishBase", "Url": "https://fishbase.co.uk", "Expect": "Welcome" }
```

The match is case-insensitive. A site with a missing `Expect` is judged on its status code only. When the text is missing, the site is down and the reason is recorded as `Expected text "..." not found`.

## Scheduled maintenance

Add planned work under `Monitor:Maintenance` in `appsettings.json`:

```json
"Maintenance": [
  { "Site": "All", "Start": "2026-11-20T22:00:00Z", "End": "2026-11-20T23:00:00Z", "Message": "Database upgrade" }
]
```

`Site` is a site name or `All`. `Start` is inclusive and `End` is exclusive, both in UTC.

During a window the sites are still checked, but those checks are excluded from uptime, incidents, and alerts. The page shows the window under "Planned maintenance" and marks affected sites as Maintenance. When nothing is scheduled, the panel says "Currently no planned maintenance." Maintenance windows are read from configuration, so a change needs a redeploy. The upcoming windows are also available at `/api/maintenance`.


## Email alerts

Alerts are off by default. Configure them under `Alerts` in `appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Turn email alerts on |
| `FailuresBeforeAlert` | `2` | Consecutive failed checks before a DOWN alert |
| `SmtpHost` / `SmtpPort` | empty / `587` | SMTP server |
| `UseStartTls` | `true` | Use STARTTLS |
| `Username` / `Password` | empty | SMTP credentials |
| `From` | empty | Sender address |
| `Recipients` | `[]` | Addresses to notify |

A DOWN alert is sent once when a site reaches the failure threshold, and a recovery alert is sent when it comes back up. Alerts are not repeated while a site stays down.

Keep the SMTP password out of source control. Supply it through an environment variable instead:

```bash
export Alerts__Password="..."
export Alerts__Enabled=true
```

## Deploying to Render (Starter plan)

1. Sign in at https://render.com with GitHub and choose **New > Web Service**.
2. Select the `severussnapz/fourteenfish-status` repository. Render detects the `Dockerfile`.
3. Choose the **Starter** instance type ($7/month) and leave the Docker settings as they are.
4. Under **Disks**, add a disk mounted at `/data`. The free plan has no disks.
5. Under **Environment**, set `Monitor__HistoryFile` to `/data/history.jsonl` so history survives restarts. Add `Alerts__Password` with the Gmail App Password and `Alerts__Enabled=true` when you want alerts live.
6. Deploy. Render gives a public `*.onrender.com` URL.

Every push to `main` triggers a redeploy, so changes to `appsettings.json` (including planned maintenance) go live on the next deploy. The history file lives on the disk, so it survives those redeploys.

Without a disk, history is lost on every restart. The free plan also sleeps after 15 minutes without traffic, which pauses checks, so use it for testing only.

## Testing

```bash
dotnet test
```
