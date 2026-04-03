namespace TastileDesktop.Tests;

public sealed class PollingServiceWallClockContractTests
{
    [Fact]
    public void PollingService_DeclaresWallClockPollingTimerField()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TastileDesktop", "Services", "PollingService.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("private readonly IWallClockPollScheduler _wallClockPollTimer;", source);
    }
}
