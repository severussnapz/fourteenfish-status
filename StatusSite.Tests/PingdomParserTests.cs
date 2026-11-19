using StatusSite;

namespace StatusSite.Tests;

public class PingdomParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private const string Page = """
        <div id="columnTwo">
            <h3>Uptime last 7 days</h3>
            <p class="large">99.99%</p>
        </div>
        <div id="columnThree">
            <h3>Avg. resp. time last 7 days</h3>
            <p class="large">561 ms</p>
        </div>
        """;

    [Fact]
    public void Parse_ReadsUptimeAndResponseTime()
    {
        var stats = PingdomParser.Parse(Page, Now);

        Assert.NotNull(stats);
        Assert.Equal(99.99, stats.Uptime7d);
        Assert.Equal(561, stats.AvgResponseMs7d);
    }

    [Fact]
    public void Parse_ReturnsNull_WhenUptimeIsMissing()
    {
        Assert.Null(PingdomParser.Parse("<html>nothing here</html>", Now));
    }

    [Fact]
    public void Parse_ReadsDailyResponseTimes_StartingOnTheGivenDate()
    {
        var html = Page + "show_responsetime_chart([2026,11,13],\n [587,498,524],\n ',', '%b %e');";

        var daily = PingdomParser.Parse(html, Now)!.DailyResponse!;

        Assert.Equal(3, daily.Count);
        Assert.Equal(new DateOnly(2026, 11, 13), daily[0].Date);
        Assert.Equal(587, daily[0].AvgResponseMs);
        Assert.Equal(new DateOnly(2026, 11, 15), daily[2].Date);
    }

    [Fact]
    public void Parse_ReadsResponseTimePerCountry()
    {
        var html = Page + "var phpArray = {\"GB\":447,\"NL\":347,\"FI\":1593};";

        var countries = PingdomParser.Parse(html, Now)!.CountryResponseMs!;

        Assert.Equal(3, countries.Count);
        Assert.Equal(447, countries["GB"]);
        Assert.Equal(1593, countries["FI"]);
    }

    [Fact]
    public void Parse_LeavesExtrasEmpty_WhenPageHasNoChartData()
    {
        var stats = PingdomParser.Parse(Page, Now)!;

        Assert.Null(stats.DailyResponse);
        Assert.Null(stats.CountryResponseMs);
    }

    [Fact]
    public void Parse_LeavesResponseTimeEmpty_WhenOnlyUptimeIsPresent()
    {
        var stats = PingdomParser.Parse("<h3>Uptime last 7 days</h3><p class=\"large\">100%</p>", Now);

        Assert.Equal(100, stats!.Uptime7d);
        Assert.Null(stats.AvgResponseMs7d);
    }
}
