using Shouldly;
using Xunit;

namespace Serilog.Sinks.TriggerBuffer.Tests;

public class SmokeTests
{
    [Fact]
    public void TestSetup_Runs()
    {
        true.ShouldBeTrue();
    }
}
