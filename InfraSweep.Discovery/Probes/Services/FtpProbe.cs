using System.Net;
using System.Net.Sockets;
using Polly;
using Polly.Retry;
using Polly.Fallback;

namespace InfraSweep.Discovery.Probes.Services;

public class FtpProbe
{
    public static async Task<string?> ProbeServerBanner(IPAddress address, int port = 21, CancellationToken token = default)
    {
        return await RetryPipeline.ExecuteAsync(
            async _ => await GetServerResponse(address, port, token),
            token
        );
    }

    private static async Task<string?> GetServerResponse(IPAddress address, int port, CancellationToken token = default)
    {
        using var client = new TcpClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        await client.ConnectAsync(address, port, cts.Token);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);

        List<string> bannerLines = [];

        string? line;
        while ((line = await reader.ReadLineAsync(cts.Token)) != null)
        {
            if (line.Length < 4)
                break;

            bannerLines.Add(line[4..]);

            if (line[3] == ' ')
                break;
        }

        if (bannerLines.Count == 0)
            return null;

        return string.Join(" ", bannerLines);
    }

    private readonly static ResiliencePipeline<string?> RetryPipeline = 
        new ResiliencePipelineBuilder<string?>()
            .AddRetry(new RetryStrategyOptions<string?>
            {
                ShouldHandle = new PredicateBuilder<string?>()
                    .Handle<SocketException>()
                    .Handle<IOException>()
                    .Handle<OperationCanceledException>()
                    .HandleResult(result => result == null),

                MaxRetryAttempts = 1,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Linear,
            })
            .AddFallback(new FallbackStrategyOptions<string?>
            {
                ShouldHandle = new PredicateBuilder<string?>()
                    .Handle<SocketException>()
                    .Handle<IOException>()
                    .Handle<OperationCanceledException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask<string?>(null)
            })
            .Build();
}