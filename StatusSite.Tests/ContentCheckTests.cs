using StatusSite;

namespace StatusSite.Tests;

public class ContentCheckTests
{
    [Fact]
    public void ContainsExpected_MatchesIgnoringCase()
    {
        Assert.True(SiteMonitor.ContainsExpected("<html><h1>Welcome to FishBase</h1></html>", "welcome to fishbase"));
    }

    [Fact]
    public void ContainsExpected_ReturnsFalse_WhenTextMissing()
    {
        Assert.False(SiteMonitor.ContainsExpected("<html>Maintenance page</html>", "Welcome"));
    }
}
