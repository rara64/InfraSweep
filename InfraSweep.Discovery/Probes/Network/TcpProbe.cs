using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Polly;
using Polly.Retry;

namespace InfraSweep.Discovery.Probes.Network;

public class TcpProbe
{
    private enum PortState
    {
        Open,
        Throttled,
        Closed
    }

    private const int maxConcurrency = 256;
    private const int minConcurrency = 2;
    private const int decreaseCooldownMilliseconds = 500;

    private class ConcurrencyState
    {
        public int currentConcurrency = minConcurrency;
        public DateTime lastDecreaseTime = DateTime.Now;
    }

    private static async Task<PortState> CheckPortState(IPAddress address, int port, CancellationToken token)
    {
        using (TcpClient client = new TcpClient())
        {
            try
            {
                await client.ConnectAsync(address, port, token);

                return PortState.Open;
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionRefused)
                    return PortState.Closed;

                return PortState.Throttled;
            }
        }
    }

    public static async Task<List<int>> GetOpenPorts(IPAddress address, int[] ports)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(ports);
        
        Queue<int> portQueue = new(ports.Shuffle());

        Channel<(PortState state, int port)> results =
            Channel.CreateUnbounded<(PortState state, int port)>();

        List<int> openPorts = [];
        List<int> dismissedPorts = [];

        ConcurrencyState concurrencyState = new();

        ResiliencePipeline<PortState> retryPipeline = RetryPipeline(concurrencyState);

        int activeWorkers = 0;

        void CreateWorkers()
        {
            int currentConcurrency = Volatile.Read(ref concurrencyState.currentConcurrency);

            while (activeWorkers < currentConcurrency && portQueue.Count > 0)
            {
                int port = portQueue.Dequeue();
                activeWorkers++;

                Task.Run(async () =>
                {
                    try
                    {
                        PortState state = await retryPipeline
                            .ExecuteAsync(async token => await CheckPortState(address, port, token));
                    
                        await results.Writer.WriteAsync((state, port));
                    }
                    catch (Exception e)
                    {
                        results.Writer.Complete(e);
                    }

                });
            }

            if (activeWorkers == 0)
                results.Writer.Complete();
        }

        CreateWorkers();

        await foreach (var (state, port) in results.Reader.ReadAllAsync())
        {
            activeWorkers--;

            switch (state)
            {
                case PortState.Open:
                    openPorts.Add(port);
                    IncreaseConcurrency(concurrencyState);
                    break;
                
                case PortState.Closed:
                    IncreaseConcurrency(concurrencyState);
                    break;
                
                case PortState.Throttled:
                    dismissedPorts.Add(port);
                    break;
            }

            CreateWorkers();
        }

        Console.WriteLine(dismissedPorts.Count);
        return openPorts;
    }

    private static ResiliencePipeline<PortState> RetryPipeline(ConcurrencyState state)
    {
        return new ResiliencePipelineBuilder<PortState>()
            .AddRetry(new RetryStrategyOptions<PortState>()
            {
                ShouldHandle = new PredicateBuilder<PortState>()
                    .HandleResult(result => result == PortState.Throttled),
                
                MaxRetryAttempts = 3,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Linear,

                OnRetry = args =>
                {
                    DecreaseConcurrency(state);
                    return default;
                }
            })
            .Build();
    }

    private static void IncreaseConcurrency(ConcurrencyState state)
    {
        lock (state)
        {
            state.currentConcurrency = Math.Min(maxConcurrency, state.currentConcurrency + 1);
        }
    }

    private static void DecreaseConcurrency(ConcurrencyState state)
    {
        lock (state)
        {
            if (DateTime.Now - state.lastDecreaseTime > TimeSpan.FromMilliseconds(decreaseCooldownMilliseconds))
            {
                state.lastDecreaseTime = DateTime.Now;
                state.currentConcurrency = Math.Max(minConcurrency, (int)Math.Ceiling(state.currentConcurrency * 0.5));
            }
        }
    }

}