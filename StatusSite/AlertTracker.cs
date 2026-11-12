namespace StatusSite;

public enum AlertKind
{
    SiteDown,
    SiteRecovered,
}

public record AlertEvent(AlertKind Kind, CheckResult Result);

// Tracks consecutive failures per site so that alerts fire only on state changes,
// not on every failed check.
public class AlertTracker
{
    private readonly int _failuresBeforeAlert;
    private readonly Dictionary<string, (int Failures, bool Down)> _state = new();
    private readonly object _lock = new();

    public AlertTracker(int failuresBeforeAlert)
    {
        _failuresBeforeAlert = Math.Max(1, failuresBeforeAlert);
    }

    public AlertEvent? Evaluate(CheckResult result)
    {
        lock (_lock)
        {
            var (failures, down) = _state.GetValueOrDefault(result.Site);

            if (result.Up)
            {
                _state[result.Site] = (0, false);
                return down ? new AlertEvent(AlertKind.SiteRecovered, result) : null;
            }

            failures++;
            var raiseAlert = !down && failures >= _failuresBeforeAlert;
            _state[result.Site] = (failures, down || raiseAlert);
            return raiseAlert ? new AlertEvent(AlertKind.SiteDown, result) : null;
        }
    }
}
