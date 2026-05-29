using System.Net.Http.Json;
using System.Text.Json;
using InfraSweep.Analysis.Models;
using Polly;
using Polly.Retry;
using Polly.Fallback;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace InfraSweep.Analysis;

public class Api {
    private static readonly Uri cpeGuesser = new("https://cpe-guesser.cve-search.org/search");
    private const string cpeBasedVulnerabilityLookup = "https://vulnerability.circl.lu/api/vulnerability/cpesearch/";
    private const string productLookup = "https://vulnerability.circl.lu/api/product/";
    private const string userAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";
    private static readonly HttpClientHandler httpHandler = new()
    {
        AllowAutoRedirect = true,
        ServerCertificateCustomValidationCallback = CertValidation.ServerCertificateValidation,
        CheckCertificateRevocationList = false,
    };
    private static readonly HttpClient client = new Func<HttpClient>(() => {
        var builder = new HttpClient(httpHandler);
        builder.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return builder;
    })();

    private static readonly IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());

    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(5))
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

    private static async Task<string> SendRequestAndGetContent(Func<HttpRequestMessage> requestFactory)
    {
        using HttpRequestMessage request = requestFactory();

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead);

        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<T?> GetAndParse<T>(Func<HttpRequestMessage> requestFactory)
    {
        string text = await SendRequestAndGetContent(requestFactory);

        return JsonSerializer.Deserialize<T>(text);
    }

    private static string GetCacheKey(string prefix, string[] parts)
    {
        string joinedParts = string.Join("|", parts
            .Select(p => p.Trim().ToLower())
            .OrderBy(x => x));

        return $"{prefix}{joinedParts}";
    }

    public static async Task<string?> GuessCpeFromQuery(string[] info, CancellationToken cancellationToken = default)
    {
        string cacheKey = GetCacheKey("cpe", info);

        if (Cache.TryGetValue(cacheKey, out string? cached))
            return cached;
        
        string? result = await RetryPipeline
            .ExecuteAsync(async innerToken => await SendRequestAndGetContent(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, cpeGuesser);
                request.Content = JsonContent.Create(new { query = info });
                return request;
            }), cancellationToken);

        if (result != null)
            Cache.Set(cacheKey, result, CacheEntryOptions);
        
        return result;
    }

    public static async Task<CpeVulnerabilitySearchResult?> GetVulnerabilitiesByCpe(string cpe, int perPage = 10, int page = 1, CancellationToken cancellationToken = default)
    {
        string cacheKey = GetCacheKey("vuln", [$"{cpe}{perPage}{page}"]);

        if (Cache.TryGetValue(cacheKey, out CpeVulnerabilitySearchResult? cached))
            return cached;
        
        string encodedCpe = Uri.EscapeDataString(cpe);

        Uri vulnerabilityLookup = 
            new ($"{cpeBasedVulnerabilityLookup}{encodedCpe}?sort_order=desc&date_sort=published&per_page={perPage}&page={page}");

        CpeVulnerabilitySearchResult? result = await RetryPipeline
            .ExecuteAsync(async token => await GetAndParse<CpeVulnerabilitySearchResult>(() =>
            {
                return new HttpRequestMessage(HttpMethod.Get, vulnerabilityLookup);
            }), cancellationToken);

        if (result != null)
            Cache.Set(cacheKey, result, CacheEntryOptions);

        return result;
    }

    public static async Task<string?> GetFirstMatchedProductFromString(string productName, CancellationToken cancellationToken = default)
    {
        string cacheKey = GetCacheKey("product", [productName]);

        if (Cache.TryGetValue(cacheKey, out string? cached))
            return cached;
        
        Uri lookupByName = new ($"{productLookup}?name={productName}");
        Uri lookupByorganization = new ($"{productLookup}?organization_name={productName}");

        foreach (Uri uri in new List<Uri>(){lookupByName, lookupByorganization})
        {
            ProductSearchResult? result = await RetryPipeline.ExecuteAsync(
                async token => await GetAndParse<ProductSearchResult>(() =>
                {
                    return new HttpRequestMessage(HttpMethod.Get, uri);
                }), cancellationToken);

            if (result != null && result.Metadata.Count != 0)
            {
                string product = result.Data.First().Name;

                Cache.Set(cacheKey, product, CacheEntryOptions);
                return product;
            }
        }

        return null;
    }

    private readonly static ResiliencePipeline<object?> RetryPipeline = 
        new ResiliencePipelineBuilder<object?>()
            .AddRetry(new RetryStrategyOptions<object?>()
            {
                ShouldHandle = new PredicateBuilder<object?>()
                    .Handle<HttpRequestException>()
                    .Handle<HttpIOException>()
                    .Handle<JsonException>()
                    .HandleResult(result => result == null),

                MaxRetryAttempts = 2,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
            })
            .AddRateLimiter(new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions()
            {
                PermitLimit = 10,
                Window = TimeSpan.FromSeconds(1),
                QueueLimit = int.MaxValue,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }))
            .AddFallback(new FallbackStrategyOptions<object?>()
            {
                ShouldHandle = new PredicateBuilder<object?>()
                    .Handle<HttpRequestException>()
                    .Handle<HttpIOException>()
                    .Handle<JsonException>(),
                FallbackAction = _ => Outcome.FromResultAsValueTask<object?>(null)
            })
            .Build();
}