using System.Net;

namespace InfraSweep.Discovery.Probes.Services;

public class HttpProbeResult
{
    public string? serverHeader;
}

public class HttpProbe
{
    public static async Task<HttpProbeResult> ProbeHTTPService (IPAddress address, int port)
    {
        HttpProbeResult result = new();

        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
            CheckCertificateRevocationList = false
        };

        using (HttpClient client = new HttpClient(handler))
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0");

            try
            {
                Uri serviceURI = new("http://" + address.ToString() + ":" + port + "/");
                HttpResponseMessage message = await client.SendAsync(
                    new HttpRequestMessage(HttpMethod.Get, serviceURI),
                    HttpCompletionOption.ResponseHeadersRead
                );
                result.serverHeader = message.Headers.Server.ToString();  
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.HttpRequestError);
                Console.WriteLine(e.StatusCode);
            }
        }

        return result;
    }
}