using System.Net;
using System.Net.Sockets;
using System.Text;
using Polly;
using Polly.Retry;
using Polly.Fallback;
using System.Buffers.Binary;

namespace InfraSweep.Discovery.Probes.Services;

public class DnsProbe
{
    private static readonly byte[] versionLabel = Encoding.ASCII.GetBytes("version");
    private static readonly byte[] bindLabel = Encoding.ASCII.GetBytes("bind");
    private static readonly byte[] dnsQueryBytes = Convert.FromHexString(
        // Query Length
        (19 + versionLabel.Length + bindLabel.Length).ToString("x4") +
        "0102" + // Transaction ID
        "0100" + // Standard Query
        "0001" + // 1 Question
        "0000" + // 0 Answer Resource Records
        "0000" + // 0 Authority Resource Records
        "0000" + // 0 Additional Resource Records
        versionLabel.Length.ToString("x2") + // 1st Label Length
        Convert.ToHexString(versionLabel) + // 1st label
        bindLabel.Length.ToString("x2") + // 2nd Label Length
        Convert.ToHexString(bindLabel) + // 2nd Label
        "00" + // End of the name
        "0010" + // Type TXT
        "0003"); // Class CHAOS 

    public static async Task<string?> ProbeServerBanner(IPAddress address, int port = 53, CancellationToken token = default)
    {
        byte[]? dnsResponse = await RetryPipeline.ExecuteAsync(
            async _ => await GetDnsServerResponse(address, port, token),
            token
        );

        if (dnsResponse == null)
            return null;

        byte[]? queryAnswer = FindQueryAnswerInResponse(dnsResponse);

        if (queryAnswer == null)
            return null;

        return Encoding.ASCII.GetString(queryAnswer);
    }

    private static async Task<byte[]?> GetDnsServerResponse(IPAddress address, int port, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        using var client = new TcpClient();

        await client.ConnectAsync(address, port, cts.Token);

        using var stream = client.GetStream();

        await stream.WriteAsync(dnsQueryBytes, 0, dnsQueryBytes.Length, cts.Token);

        byte[] lengthBuf = new byte[2];
        await stream.ReadExactlyAsync(lengthBuf, cts.Token);

        int responseSize = BinaryPrimitives.ReadUInt16BigEndian(lengthBuf);

        byte[] responseBuffer = new byte[responseSize];
        await stream.ReadExactlyAsync(responseBuffer, cts.Token);

        return responseBuffer;
    }

    private static byte[]? FindQueryAnswerInResponse(byte[] response)
    {
        // Find repeated query in the server response
        int questionIndex = response.IndexOf(dnsQueryBytes[14..]);

        if (questionIndex == -1)
            return null;

        byte[] responseAfterQuestion = response[(questionIndex + dnsQueryBytes[14..].Length)..];

        // Find TXT CHAOS record after the repated query
        int txtRecordIndex = responseAfterQuestion.IndexOf(dnsQueryBytes[^4..]);

        if (txtRecordIndex == -1)
            return null;

        byte[] responseAfterTxtRecord = responseAfterQuestion[(txtRecordIndex + dnsQueryBytes[^4..].Length)..];

        // Response will have 1 byte with the text length
        // and then the text follows
        int txtLength = responseAfterTxtRecord[6];
        return responseAfterTxtRecord[7..(7 + txtLength)];
    }

    private readonly static ResiliencePipeline<byte[]?> RetryPipeline = 
        new ResiliencePipelineBuilder<byte[]?>()
            .AddRetry(new RetryStrategyOptions<byte[]?>
            {
                ShouldHandle = new PredicateBuilder<byte[]?>()
                    .Handle<SocketException>()
                    .Handle<IOException>()
                    .Handle<OperationCanceledException>()
                    .HandleResult(result => result == null),

                MaxRetryAttempts = 1,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Linear,
            })
            .AddFallback(new FallbackStrategyOptions<byte[]?>
            {
                ShouldHandle = new PredicateBuilder<byte[]?>()
                    .Handle<SocketException>()
                    .Handle<IOException>()
                    .Handle<OperationCanceledException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask<byte[]?>(null)
            })
            .Build();
}