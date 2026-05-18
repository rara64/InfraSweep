using System.Net;

namespace InfraSweep.Discovery.Probes.Services;

public class HttpProbe
{
    private static string userAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";
    private static int timeoutSeconds = 4;
    private static HttpClientHandler httpHandler = new()
    {
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        CheckCertificateRevocationList = false
    };

    public static async Task<String?> ProbeServerHeader (IPAddress address, int port)
    {
        using (HttpClient client = new HttpClient(httpHandler))
        {
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

            try
            {
                foreach (string method in new string[]{"http","https"})
                {
                    Uri serviceUri = new($"{method}://{address.ToString()}:{port}");
                    
                    HttpResponseMessage response = await client.SendAsync(
                        new HttpRequestMessage(HttpMethod.Get, serviceUri),
                        HttpCompletionOption.ResponseHeadersRead
                    );

                    return response.Headers.Server.ToString();
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.HttpRequestError);
                Console.WriteLine(e.StatusCode);
            }
        }

        return null;
    }
}