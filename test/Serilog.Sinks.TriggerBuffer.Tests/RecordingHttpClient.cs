using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Serilog.Sinks.TriggerBuffer.Tests;

/// <summary>
/// Stands in for the HTTP transport of Serilog.Sinks.Http, so no network is involved. It records the request bodies
/// by request URI. It must be public with a default constructor so that JSON configuration can create it by type name.
/// </summary>
public sealed class RecordingHttpClient : IHttpClient
{
    private static readonly ConcurrentQueue<(string RequestUri, string Body)> Requests = new();

    public void Configure(IConfiguration configuration)
    {
    }

    public void Dispose()
    {
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        Stream contentStream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(contentStream, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Requests.Enqueue((requestUri, body));
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    /// <summary>The rendered messages of all events that were posted to the request URI, in the order posted.</summary>
    internal static IReadOnlyList<string> RenderedMessagesPostedTo(string requestUri)
    {
        var messages = new List<string>();
        foreach (var (uri, body) in Requests.ToArray())
        {
            if (uri != requestUri)
            {
                continue;
            }

            using var document = JsonDocument.Parse(body);
            foreach (var logEvent in document.RootElement.EnumerateArray())
            {
                messages.Add(logEvent.GetProperty("RenderedMessage").GetString() ?? string.Empty);
            }
        }

        return messages;
    }
}
