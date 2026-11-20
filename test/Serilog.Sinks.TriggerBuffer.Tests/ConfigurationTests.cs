using System.Text;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace Serilog.Sinks.TriggerBuffer.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Json_configuration_with_a_nested_target_behaves_like_the_csharp_configuration()
    {
        var csharpTarget = NewTargetName();
        var jsonTarget = NewTargetName();

        var csharpLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TriggerBuffer(
                t => t.Recording(csharpTarget),
                bufferMinimumLevel: LogEventLevel.Debug,
                triggerLevel: LogEventLevel.Warning,
                maximumBufferCount: 2,
                maximumBufferAge: TimeSpan.FromSeconds(30),
                flushOnShutdown: true)
            .CreateLogger();

        var jsonLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(FromJson($$"""
                {
                  "Serilog": {
                    "Using": [ "Serilog.Sinks.TriggerBuffer", "Serilog.Sinks.TriggerBuffer.Tests" ],
                    "MinimumLevel": "Verbose",
                    "WriteTo": [
                      {
                        "Name": "TriggerBuffer",
                        "Args": {
                          "bufferMinimumLevel": "Debug",
                          "triggerLevel": "Warning",
                          "maximumBufferCount": 2,
                          "maximumBufferAge": "00:00:30",
                          "flushOnShutdown": true,
                          "configureTarget": [
                            { "Name": "Recording", "Args": { "name": "{{jsonTarget}}" } }
                          ]
                        }
                      }
                    ]
                  }
                }
                """))
            .CreateLogger();

        RunScenario(csharpLogger);
        RunScenario(jsonLogger);

        var expected = new[] { "debug 2", "debug 3", "warning trigger", "rest" };
        RecordingSinkExtensions.Get(csharpTarget).Messages.ShouldBe(expected);
        RecordingSinkExtensions.Get(jsonTarget).Messages.ShouldBe(expected);
    }

    [Fact]
    public void Json_configuration_without_options_uses_the_defaults()
    {
        var target = NewTargetName();
        using var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(FromJson($$"""
                {
                  "Serilog": {
                    "Using": [ "Serilog.Sinks.TriggerBuffer", "Serilog.Sinks.TriggerBuffer.Tests" ],
                    "MinimumLevel": "Verbose",
                    "WriteTo": [
                      {
                        "Name": "TriggerBuffer",
                        "Args": {
                          "configureTarget": [
                            { "Name": "Recording", "Args": { "name": "{{target}}" } }
                          ]
                        }
                      }
                    ]
                  }
                }
                """))
            .CreateLogger();

        logger.Verbose("verbose context");
        logger.Warning("warning context");
        RecordingSinkExtensions.Get(target).Messages.ShouldBeEmpty();

        logger.Error("trigger");

        RecordingSinkExtensions.Get(target).Messages.ShouldBe(["verbose context", "warning context", "trigger"]);
    }

    [Theory]
    [InlineData("\"maximumBufferCount\": 0", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"maximumBufferAge\": \"00:00:00\"", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"bufferMinimumLevel\": \"Fatal\", \"triggerLevel\": \"Error\"", typeof(ArgumentException))]
    public void Invalid_json_values_are_rejected_when_the_logger_is_created(string invalidArguments, Type expected)
    {
        var target = NewTargetName();
        var configuration = FromJson($$"""
            {
              "Serilog": {
                "Using": [ "Serilog.Sinks.TriggerBuffer", "Serilog.Sinks.TriggerBuffer.Tests" ],
                "WriteTo": [
                  {
                    "Name": "TriggerBuffer",
                    "Args": {
                      {{invalidArguments}},
                      "configureTarget": [
                        { "Name": "Recording", "Args": { "name": "{{target}}" } }
                      ]
                    }
                  }
                ]
              }
            }
            """);

        var exception = Should.Throw<Exception>(() => new LoggerConfiguration().ReadFrom.Configuration(configuration));

        InnerExceptions(exception).ShouldContain(e => expected.IsInstanceOfType(e));
    }

    private static IEnumerable<Exception> InnerExceptions(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static void RunScenario(Logger logger)
    {
        logger.Verbose("verbose is below the buffer minimum level");
        logger.Debug("debug 1");
        logger.Debug("debug 2");
        logger.Debug("debug 3");
        logger.Warning("warning trigger");
        logger.Debug("rest");
        logger.Dispose();
    }

    private static string NewTargetName() => Guid.NewGuid().ToString("N");

    private static IConfiguration FromJson(string json)
    {
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();
    }
}
