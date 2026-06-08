using System.Net;
using System.Net.Sockets;
using Polly;
using Polly.Retry;
using Polly.Fallback;

namespace InfraSweep.Discovery.Probes.Services;

public class SshProbe
{
    public static async Task<string?> ProbeServerBanner(IPAddress address, int port = 22, CancellationToken token = default)
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

        return await reader.ReadLineAsync(cts.Token);
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