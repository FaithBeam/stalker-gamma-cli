using stalker_gamma_cli.Utilities;

namespace stalker_gamma_cli.Tests;

public class ProgressThrottleUtilityTests
{
    [Fact]
    public void Reports_the_first_update_and_drops_the_rest_within_the_interval()
    {
        List<double> reported = [];
        var throttled = ProgressThrottleUtility.Throttle<double>(
            reported.Add,
            TimeSpan.FromHours(1)
        );

        throttled(0.1);
        throttled(0.2);
        throttled(0.3);

        Assert.Equal([0.1], reported);
    }

    [Fact]
    public void Always_reports_completion()
    {
        List<double> reported = [];
        var throttled = ProgressThrottleUtility.Throttle<double>(
            reported.Add,
            TimeSpan.FromHours(1)
        );

        throttled(0.1);
        throttled(1.0);

        Assert.Equal([0.1, 1.0], reported);
    }

    [Fact]
    public void Reports_every_update_with_no_interval()
    {
        List<double> reported = [];
        var throttled = ProgressThrottleUtility.Throttle<double>(reported.Add, TimeSpan.Zero);

        throttled(0.1);
        throttled(0.2);

        Assert.Equal([0.1, 0.2], reported);
    }
}
