using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Polly;
using Polly.CircuitBreaker;
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
        public int CurrentConcurrency = minConcurrency;
        public DateTime LastDecreaseTime = DateTime.Now;
        public int ActiveWorkers = 0;
    }

    private static async Task<PortState> CheckPortState(
        IPAddress address, 
        int port, 
        SemaphoreSlim? globalBudget = null,
        CancellationToken cancellationToken = default,
        int timeoutSeconds = 20)
    {
        await (globalBudget?.WaitAsync(cancellationToken) ?? Task.CompletedTask);

        using TcpClient client = new();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await client.ConnectAsync(address, port, cts.Token);

            return PortState.Open;
        }
        catch (OperationCanceledException e) when 
            (e.CancellationToken == cts.Token
            && !cancellationToken.IsCancellationRequested)
        {
            return PortState.Throttled;
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.ConnectionRefused)
                return PortState.Closed;
            else if (e.SocketErrorCode == SocketError.HostUnreachable)
                throw;

            return PortState.Throttled;
        }
        finally
        {
            globalBudget?.Release();
        }
    }

    private static async Task RunWorker(
        IPAddress address,
        int port, 
        Channel<(PortState, int)> results,
        SemaphoreSlim? globalBudget,
        ResiliencePipeline<PortState> retryPipeline,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PortState portState = await retryPipeline
                .ExecuteAsync(async token => 
                    await CheckPortState(address, port, globalBudget, token),
                    cancellationToken);

            await results.Writer.WriteAsync((portState, port));
        }
        catch (Exception e) when
            (e is OperationCanceledException
            or BrokenCircuitException)
        {
            results.Writer.TryComplete();
        }
        catch (SocketException e) when 
            (e.SocketErrorCode == SocketError.HostUnreachable)
        {
            results.Writer.TryComplete();
        }
        catch (Exception e)
        {
            results.Writer.TryComplete(e);
        }
    }

    public static async Task<List<int>> GetOpenPorts(
        IPAddress address, 
        int[] ports, 
        SemaphoreSlim? globalBudget = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(ports);

        Queue<int> portQueue = new(ports.Shuffle());

        Channel<(PortState portState, int port)> results =
            Channel.CreateUnbounded<(PortState portState, int port)>();

        List<int> openPorts = [];
        List<int> dismissedPorts = [];

        ConcurrencyState state = new();

        ResiliencePipeline<PortState> retryPipeline = RetryPipeline(state);

        void CreateWorkers()
        {
            List<int> dequeuedPorts = [];
            bool isComplete = false;

            lock (state)
            {
                if (results.Reader.Completion.IsCompleted)
                    return;
                
                while (state.ActiveWorkers < state.CurrentConcurrency 
                    && portQueue.Count > 0 
                    && !cancellationToken.IsCancellationRequested)
                {
                    dequeuedPorts.Add(portQueue.Dequeue());
                    state.ActiveWorkers++;
                }

                isComplete = state.ActiveWorkers == 0;
            }

            foreach (int port in dequeuedPorts)
                _= RunWorker(address, port, results, globalBudget, retryPipeline, cancellationToken);

            if (isComplete)
                results.Writer.TryComplete();
        }

        CreateWorkers();

        await foreach (var (portState, port) in results.Reader.ReadAllAsync(cancellationToken))
        {
            lock (state)
            {
                state.ActiveWorkers--;
            }

            switch (portState)
            {
                case PortState.Open:
                    openPorts.Add(port);
                    IncreaseConcurrency(state);
                    break;

                case PortState.Closed:
                    IncreaseConcurrency(state);
                    break;

                case PortState.Throttled:
                    dismissedPorts.Add(port);
                    break;
            }

            CreateWorkers();
        }

        if (dismissedPorts.Count < 5)
            foreach (int port in dismissedPorts)
            {
                try
                {
                    var result = await CheckPortState(address, port, globalBudget, cancellationToken, 2);
                    if (result == PortState.Open)
                        openPorts.Add(port);   
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.HostUnreachable)
                {
                    return openPorts;
                }
            }

        return openPorts;
    }

    private static ResiliencePipeline<PortState> RetryPipeline(ConcurrencyState state)
    {
        return new ResiliencePipelineBuilder<PortState>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<PortState>
            {
                ShouldHandle = new PredicateBuilder<PortState>()
                    .HandleResult(result => result == PortState.Throttled),
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 2
            })
            .AddRetry(new RetryStrategyOptions<PortState>
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
            state.CurrentConcurrency = Math.Min(maxConcurrency, state.CurrentConcurrency + 1);
        }
    }

    private static void DecreaseConcurrency(ConcurrencyState state)
    {
        lock (state)
        {
            if (DateTime.Now - state.LastDecreaseTime > TimeSpan.FromMilliseconds(decreaseCooldownMilliseconds))
            {
                state.LastDecreaseTime = DateTime.Now;
                state.CurrentConcurrency = Math.Max(minConcurrency, (int)Math.Ceiling(state.CurrentConcurrency * 0.5));
            }
        }
    }
}