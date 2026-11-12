using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StatusSite;

public interface IAlertSender
{
    Task SendAsync(string subject, string body, CancellationToken ct);
}

public class EmailAlertSender : IAlertSender
{
    private readonly AlertOptions _options;

    public EmailAlertSender(IOptions<AlertOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        foreach (var recipient in _options.Recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var security = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, security, ct);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? "", ct);
        }

        await client.SendAsync(message, cancellationToken: ct);
        await client.DisconnectAsync(true, ct);
    }
}

public class AlertService
{
    private readonly AlertTracker _tracker;
    private readonly IAlertSender _sender;
    private readonly AlertOptions _options;
    private readonly ILogger<AlertService> _logger;

    public AlertService(AlertTracker tracker, IAlertSender sender, IOptions<AlertOptions> options, ILogger<AlertService> logger)
    {
        _tracker = tracker;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(CheckResult result, CancellationToken ct)
    {
        var alert = _tracker.Evaluate(result);
        if (alert is null)
        {
            return;
        }

        var (subject, body) = Compose(alert);
        if (!_options.Enabled)
        {
            _logger.LogWarning("Alert not sent (alerts disabled): {Subject}", subject);
            return;
        }

        try
        {
            await _sender.SendAsync(subject, body, ct);
            _logger.LogInformation("Alert sent: {Subject}", subject);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to send alert: {Subject}", subject);
        }
    }

    public static (string Subject, string Body) Compose(AlertEvent alert)
    {
        var r = alert.Result;
        var checkedAt = r.CheckedAt.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var detail = r.Error is { } error
            ? $"Error: {error}"
            : $"HTTP status: {r.StatusCode}";

        return alert.Kind switch
        {
            AlertKind.SiteDown => (
                $"[DOWN] {r.Site} is down",
                $"{r.Site} failed its status check.\n\nChecked at: {checkedAt}\n{detail}\n\nSite: {r.Site}"),
            AlertKind.SiteRecovered => (
                $"[RECOVERED] {r.Site} is back up",
                $"{r.Site} is responding again.\n\nChecked at: {checkedAt}\nHTTP status: {r.StatusCode}"),
            _ => throw new ArgumentOutOfRangeException(nameof(alert)),
        };
    }
}
