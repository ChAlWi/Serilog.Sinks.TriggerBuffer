using Serilog.Sinks.Http;
using Shouldly;
using Xunit;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>
/// Checks that the sink can wrap the DurableHttp target of Serilog.Sinks.Http: construction, configuration and
/// regular closing. The HTTP transport is a stand-in, so these tests do not prove delivery to a real receiver.
/// </summary>
public class DurableHttpIntegrationTests
{
    private static readonly TimeSpan WaitForShipping = TimeSpan.FromSeconds(10);

    [Fact]
    public void The_durable_http_target_can_be_wrapped_and_is_closed_when_the_logger_is_disposed()
    {
        using var directory = new TemporaryDirectory();
        var requestUri = NewRequestUri();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(t => t.DurableHttpUsingFileSizeRolledBuffers(
                requestUri: requestUri,
                bufferBaseFileName: System.IO.Path.Combine(directory.Path, "buffer"),
                period: TimeSpan.FromMilliseconds(100),
                httpClient: new RecordingHttpClient()))
            .CreateLogger();

        logger.Information("context");
        logger.Error("trigger");
        Should.NotThrow(() => logger.Dispose());

        // Closing the target releases its buffer files. A file that is still open cannot be deleted on Windows.
        Should.NotThrow(() => Directory.Delete(directory.Path, recursive: true));
        SpinWait.SpinUntil(() => RecordingHttpClient.RenderedMessagesPostedTo(requestUri).Count >= 2, WaitForShipping);
        RecordingHttpClient.RenderedMessagesPostedTo(requestUri).ShouldBe(["context", "trigger"]);
    }

    [Fact]
    public void The_durable_http_target_can_be_nested_in_json_configuration()
    {
        using var directory = new TemporaryDirectory();
        var requestUri = NewRequestUri();
        var bufferBaseFileName = System.IO.Path.Combine(directory.Path, "buffer").Replace('\\', '/');

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(ConfigurationTests.FromJson($$"""
                {
                  "Serilog": {
                    "Using": [ "Serilog.Sinks.TriggerBuffer", "Serilog.Sinks.Http" ],
                    "MinimumLevel": "Verbose",
                    "WriteTo": [
                      {
                        "Name": "TriggerBuffer",
                        "Args": {
                          "configureTarget": [
                            {
                              "Name": "DurableHttpUsingFileSizeRolledBuffers",
                              "Args": {
                                "requestUri": "{{requestUri}}",
                                "bufferBaseFileName": "{{bufferBaseFileName}}",
                                "bufferFileSizeLimitBytes": 100000,
                                "retainedBufferFileCountLimit": 3,
                                "period": "00:00:00.100",
                                "httpClient": "Serilog.Sinks.TriggerBuffer.Tests.RecordingHttpClient, Serilog.Sinks.TriggerBuffer.Tests"
                              }
                            }
                          ]
                        }
                      }
                    ]
                  }
                }
                """))
            .CreateLogger();

        logger.Information("context");
        logger.Error("trigger");
        Should.NotThrow(() => logger.Dispose());

        Should.NotThrow(() => Directory.Delete(directory.Path, recursive: true));
        SpinWait.SpinUntil(() => RecordingHttpClient.RenderedMessagesPostedTo(requestUri).Count >= 2, WaitForShipping);
        RecordingHttpClient.RenderedMessagesPostedTo(requestUri).ShouldBe(["context", "trigger"]);
    }

    private static string NewRequestUri() => "http://localhost.invalid/ingest-" + Guid.NewGuid().ToString("N");
}
