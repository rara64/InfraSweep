using System.Net;
using System.Text.RegularExpressions;
using Polly;
using Polly.Fallback;
using Polly.Retry;

namespace InfraSweep.Discovery.Probes.Services;

public class HttpProbe
{
    private const string userAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";
    private static readonly HttpClientHandler httpHandler = new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 4,
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        CheckCertificateRevocationList = false,
    };
    private static readonly HttpClient client = new Func<HttpClient>(() => {
        var builder = new HttpClient(httpHandler);
        builder.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return builder;
    })();

    public static async Task<string?> ProbeServerBanner(IPAddress address, int port, CancellationToken token = default)
    {
        using var response = await RetryPipeline.ExecuteAsync(
            async _ => await GetServerResponse(address, port, token),
            token
        );

        if (response == null)
            return null;

        string? serverHeader = response.Headers.Server?.ToString();

        if (serverHeader?.Trim().Length > 0)
            return serverHeader;

        string html = await response.Content.ReadAsStringAsync(token);

        var match = Regex.Match(html, "<title>(?<title>.*?)</title>");
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["title"].Value))
            return match.Groups["title"].Value;

        return null;
    }

    private static async Task<HttpResponseMessage?> GetServerResponse(IPAddress address, int port, CancellationToken token = default)
    {
        foreach (string method in new string[] { "http", "https" })
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            Uri serviceUri = new($"{method}://{address}:{port}");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, serviceUri);
                
                var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    cts.Token);

                return response;
            }
            catch (OperationCanceledException e) when (
                e.CancellationToken == cts.Token 
                && !token.IsCancellationRequested)
            {
                continue;
            }
        }

        return null;
    }

    private readonly static ResiliencePipeline<HttpResponseMessage?> RetryPipeline = 
        new ResiliencePipelineBuilder<HttpResponseMessage?>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage?>()
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage?>()
                    .Handle<HttpRequestException>()
                    .Handle<HttpIOException>()
                    .HandleResult(result => result == null),

                MaxRetryAttempts = 1,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Linear,
            })
            .AddFallback(new FallbackStrategyOptions<HttpResponseMessage?>()
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage?>()
                    .Handle<HttpRequestException>()
                    .Handle<HttpIOException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask<HttpResponseMessage?>(null)
            })
            .Build();
}